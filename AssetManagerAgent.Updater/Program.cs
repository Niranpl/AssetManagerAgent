using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string embeddedPublicKey = @"
-----BEGIN PUBLIC KEY-----
MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEArLns9zFtqlfGLPjYXMEM
iox6U8lDu/esMBq+wX1seKwiuVKrGep+OWKTYtJ9BWhvhFCRBkIJPFTRNMKC9a2B
JQFHLjPK/k2GFHOcMFssnUcq42U1uANOVj3xjKoTCoKjk5mYvo+HNnzXwsJ2yUfh
YmHrHyEgJizE8J6BRpmH9Oz6gwkx3KjTH7OAaC+hmq8MO+ULeN2TZfyoVxMicnqw
DyqcxR/dBZVCzvVXcQ8RBtLNC9xNM7rGFgbIzW/4SKHP8TPSnKiC6HP2fumxYIxd
rT88ducsos5+RE1hqs2BO9DU0ortkk6Cx+3MgVZ/OrkKzOINyyjgpwVLchKIeM86
nkyAufselL+7HqQNsbpB4NflywONdOh9px+ryJeVbAAo3G0OPOwofPPnK88vJ/OY
f/bePIm2T+/2UzEqObKBTo1yV7pGDfsi8RHrG74CqDALgbR2ZybEw3XKKM/qHsdA
I39HVOd9fUckplWBhdV8/HzjPcZwKI64V342qJDq5efBAgMBAAE=
-----END PUBLIC KEY-----
";


if (args.Length == 1 && args[0] == "github-verify-test")
{
    try
    {
        const string baseUrl =
            "https://github.com/Niranpl/AHT-AssetManager-Updates/releases/download/v2.0.1-test";

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        async Task<byte[]> DownloadSmallFile(string initialUrl)
        {
            Uri current = new Uri(initialUrl);

            for (int redirects = 0; ; redirects++)
            {
                if (current.Scheme != Uri.UriSchemeHttps ||
                    !string.IsNullOrEmpty(current.UserInfo))
                    throw new InvalidDataException(
                        "Only HTTPS URLs without embedded credentials are allowed.");

                string host = current.IdnHost.ToLowerInvariant();

                if (host != "github.com" &&
                    host != "release-assets.githubusercontent.com")
                    throw new InvalidDataException(
                        "Redirect destination host is not allowed.");

                using var response = await client.GetAsync(
                    current, HttpCompletionOption.ResponseHeadersRead);

                if ((int)response.StatusCode >= 300 &&
                    (int)response.StatusCode < 400)
                {
                    if (redirects >= 3)
                        throw new InvalidDataException(
                            "Redirect limit exceeded.");

                    Uri? location = response.Headers.Location;

                    if (location == null)
                        throw new InvalidDataException(
                            "Redirect did not provide a destination.");

                    current = location.IsAbsoluteUri
                        ? location
                        : new Uri(current, location);

                    continue;
                }

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long length &&
                    length > 1024 * 1024)
                    throw new InvalidDataException(
                        "File exceeds the 1 MB limit.");

                using var output = new MemoryStream();
                byte[] buffer = new byte[8192];
                int read;

                while ((read = await response.Content.ReadAsStreamAsync()
                    .ContinueWith(async task =>
                    {
                        using Stream input = await task;
                        return await input.ReadAsync(buffer, 0, buffer.Length);
                    }).Unwrap()) > 0)
                {
                    output.Write(buffer, 0, read);

                    if (output.Length > 1024 * 1024)
                        throw new InvalidDataException(
                            "File exceeds the 1 MB limit.");
                }

                return output.ToArray();
            }
        }

        byte[] manifestBytes = await DownloadSmallFile(
            baseUrl + "/github-release-manifest.json");

        byte[] signatureBytes = await DownloadSmallFile(
            baseUrl + "/github-release-manifest.sig");

        string signatureText = Encoding.ASCII.GetString(signatureBytes).Trim();
        byte[] signature = Convert.FromBase64String(signatureText);

        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;

        string version = root.GetProperty("version").GetString() ?? "";
        string url = root.GetProperty("packageUrl").GetString() ?? "";
        string hash = root.GetProperty("sha256").GetString() ?? "";

        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
            throw new InvalidDataException("Invalid version.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? packageUri) ||
            packageUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(packageUri.UserInfo))
            throw new InvalidDataException("Package URL must use HTTPS.");

        if (!Regex.IsMatch(hash, @"\A[0-9A-Fa-f]{64}\z"))
            throw new InvalidDataException("Invalid SHA-256 value.");

        using RSA verifier = RSA.Create();
        verifier.ImportFromPem(embeddedPublicKey);

        if (!verifier.VerifyData(
            manifestBytes, signature, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss))
            throw new CryptographicException("Manifest signature is invalid.");

        Console.WriteLine("GitHub manifest signature: VALID");
        Console.WriteLine($"Release version: {version}");
        Console.WriteLine("Package URL: HTTPS");
        Console.WriteLine("Package SHA-256 format: VALID");
        Console.WriteLine("Test only; no package download or installation performed.");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"GitHub verification failed: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

if (args.Length == 1 && args[0] == "github-download-test")
{
    try
    {
        const string baseUrl =
            "https://github.com/Niranpl/AHT-AssetManager-Updates/releases/download/v2.0.1-test";
        const string manifestName = "github-release-manifest.json";
        const string signatureName = "github-release-manifest.sig";
        const long maxPackageBytes = 100L * 1024 * 1024;

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        async Task<byte[]> DownloadBytes(string initialUrl, long maxBytes)
        {
            Uri current = new Uri(initialUrl);

            for (int redirects = 0; ; redirects++)
            {
                if (current.Scheme != Uri.UriSchemeHttps ||
                    !string.IsNullOrEmpty(current.UserInfo))
                    throw new InvalidDataException("Only HTTPS URLs without credentials are allowed.");

                string host = current.IdnHost.ToLowerInvariant();
                if (host != "github.com" &&
                    host != "release-assets.githubusercontent.com")
                    throw new InvalidDataException("Download host is not allowed.");

                using var response = await client.GetAsync(
                    current, HttpCompletionOption.ResponseHeadersRead);

                if ((int)response.StatusCode >= 300 &&
                    (int)response.StatusCode < 400)
                {
                    if (redirects >= 3)
                        throw new InvalidDataException("Redirect limit exceeded.");

                    Uri? location = response.Headers.Location;
                    if (location == null)
                        throw new InvalidDataException("Redirect destination is missing.");

                    current = location.IsAbsoluteUri
                        ? location
                        : new Uri(current, location);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long length &&
                    length > maxBytes)
                    throw new InvalidDataException("Download exceeds the size limit.");

                await using Stream input = await response.Content.ReadAsStreamAsync();
                using var output = new MemoryStream();
                byte[] buffer = new byte[81920];
                int read;

                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > maxBytes)
                        throw new InvalidDataException("Download exceeds the size limit.");

                    output.Write(buffer, 0, read);
                }

                return output.ToArray();
            }
        }

        byte[] manifestBytes = await DownloadBytes(
            baseUrl + "/" + manifestName, 1024 * 1024);
        byte[] signatureBytes = await DownloadBytes(
            baseUrl + "/" + signatureName, 1024 * 1024);

        string signatureText = Encoding.ASCII.GetString(signatureBytes).Trim();
        byte[] signature = Convert.FromBase64String(signatureText);

        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;

        string version = root.GetProperty("version").GetString() ?? "";
        string packageUrl = root.GetProperty("packageUrl").GetString() ?? "";
        string expectedHash = root.GetProperty("sha256").GetString() ?? "";

        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
            throw new InvalidDataException("Invalid release version.");

        using RSA verifier = RSA.Create();
        verifier.ImportFromPem(embeddedPublicKey);

        if (!verifier.VerifyData(
            manifestBytes, signature, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss))
            throw new CryptographicException("Manifest signature is invalid.");

        if (!Regex.IsMatch(expectedHash, @"\A[0-9A-Fa-f]{64}\z"))
            throw new InvalidDataException("Invalid package SHA-256.");

        if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out Uri? packageUri) ||
            packageUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(packageUri.UserInfo) ||
            !string.Equals(packageUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !packageUri.AbsolutePath.Contains(
                "/Niranpl/AHT-AssetManager-Updates/releases/download/",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Package URL is not an approved GitHub release URL.");

        Console.WriteLine("Manifest signature: VALID");
        Console.WriteLine($"Release version: {version}");
        Console.WriteLine("Downloading package...");

        byte[] packageBytes = await DownloadBytes(packageUrl, maxPackageBytes);

        byte[] actualHash = SHA256.HashData(packageBytes);
        byte[] expectedHashBytes = Convert.FromHexString(expectedHash);

        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHashBytes))
            throw new CryptographicException("Downloaded package SHA-256 does not match.");

        string outputDirectory = @"C:\AssetManagerAgent\Updater-Test";
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(
            outputDirectory, $"aht-agent-{version}-verified.zip");
        string temporaryPath = outputPath + ".download";

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, packageBytes);
            File.Move(temporaryPath, outputPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        Console.WriteLine("Package SHA-256: VALID");
        Console.WriteLine($"Verified package saved: {outputPath}");
        Console.WriteLine("Test only; no installation or agent changes performed.");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"GitHub download test failed: {ex.Message}");
        Environment.ExitCode = 1;
    }

    return;
}
if (args.Length == 3 && args[0] == "verify-package")
{
    try
    {
        string packagePath = Path.GetFullPath(args[1]);
        string expectedHash = args[2];

        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Package file not found.");

        if (!Regex.IsMatch(expectedHash, @"\A[0-9A-Fa-f]{64}\z"))
            throw new InvalidDataException("Expected SHA-256 must be 64 hexadecimal characters.");

        using var stream = File.OpenRead(packagePath);
        byte[] hashBytes = SHA256.HashData(stream);
        string actualHash = Convert.ToHexString(hashBytes);

        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(actualHash),
            Convert.FromHexString(expectedHash)))
        {
            throw new CryptographicException("Package SHA-256 does not match.");
        }

        Console.WriteLine("Package SHA-256: VALID");
        Console.WriteLine($"File: {Path.GetFileName(packagePath)}");
        Console.WriteLine("Verification only; no installation performed.");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Package rejected: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}


if (args.Length == 2 && args[0] == "fetch-test")
{
    const string manifestUrl =
        "https://withered-bush-674a.niran-pl.workers.dev/manifest";

    string outputPath = Path.GetFullPath(args[1]);

    try
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        using var response = await client.GetAsync(
            manifestUrl,
            HttpCompletionOption.ResponseHeadersRead);

        if ((int)response.StatusCode >= 300 &&
            (int)response.StatusCode < 400)
        {
            throw new InvalidDataException(
                "Redirects are not allowed.");
        }

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long length &&
            length > 1024 * 1024)
        {
            throw new InvalidDataException(
                "Manifest exceeds the 1 MB limit.");
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();

        if (bytes.Length > 1024 * 1024)
        {
            throw new InvalidDataException(
                "Manifest exceeds the 1 MB limit.");
        }

        using JsonDocument document = JsonDocument.Parse(bytes);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Manifest must be a JSON object.");
        }

        string? directory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(outputPath, bytes);

        Console.WriteLine("Manifest downloaded successfully.");
        Console.WriteLine($"Saved to: {outputPath}");
        Console.WriteLine(
            "UNTRUSTED: Signature verification is still required.");
        return;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"Manifest download failed: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}


if (args.Length == 3 && args[0] == "sign-test")
{
    string manifestPath = Path.GetFullPath(args[1]);
    string signaturePath = Path.GetFullPath(args[2]);
    string privatePath = @"C:\AssetManagerAgent\UpdateSigning\release-private.pem";

    if (!File.Exists(manifestPath) || !File.Exists(privatePath))
        throw new FileNotFoundException("Test manifest or private key is missing.");

    using RSA signer = RSA.Create();
    signer.ImportFromPem(File.ReadAllText(privatePath));

    byte[] bytes = File.ReadAllBytes(manifestPath);
    byte[] signature = signer.SignData(
        bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

    File.WriteAllText(signaturePath, Convert.ToBase64String(signature), Encoding.ASCII);
    Console.WriteLine("Test manifest signed successfully.");
    return;
}

if (args.Length != 3 || args[0] != "verify")
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  sign-test <manifest.json> <manifest.sig>");
    Console.WriteLine("  verify <manifest.json> <manifest.sig>");
	Console.WriteLine("  fetch-test <output.json>");
    return;
}

string manifest = Path.GetFullPath(args[1]);
string sigPath = Path.GetFullPath(args[2]);

try
{
    if (!File.Exists(manifest) || !File.Exists(sigPath))
        throw new FileNotFoundException("Manifest, signature, or public key is missing.");

    byte[] bytes = File.ReadAllBytes(manifest);
    byte[] signature = Convert.FromBase64String(File.ReadAllText(sigPath).Trim());

    using JsonDocument doc = JsonDocument.Parse(bytes);
    JsonElement root = doc.RootElement;

    string version = root.GetProperty("version").GetString() ?? "";
    string url = root.GetProperty("packageUrl").GetString() ?? "";
    string hash = root.GetProperty("sha256").GetString() ?? "";

    if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$"))
        throw new InvalidDataException("Invalid version.");

    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
        uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        throw new InvalidDataException("Package URL must use HTTPS without embedded credentials.");

    if (!Regex.IsMatch(hash, @"\A[0-9A-Fa-f]{64}\z"))
        throw new InvalidDataException("Invalid SHA-256 value.");

    using RSA verifier = RSA.Create();



    verifier.ImportFromPem(embeddedPublicKey);

    if (!verifier.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
        throw new CryptographicException("Manifest signature is invalid.");

    Console.WriteLine("Manifest signature: VALID");
    Console.WriteLine($"Release version: {version}");
    Console.WriteLine("Package URL: HTTPS");
    Console.WriteLine("Package SHA-256 format: VALID");
    Console.WriteLine("Verification only; no download or installation performed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Manifest rejected: {ex.Message}");
    Environment.ExitCode = 1;
}

