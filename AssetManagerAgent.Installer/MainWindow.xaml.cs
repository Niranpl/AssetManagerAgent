
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AssetManagerAgent.Installer;

public partial class MainWindow : Window
{
    private const string PayloadPath =
        @"C:\AssetManagerAgent\Installer\Payload";

    private const string InstallPath =
        @"C:\Program Files\AHTAgent";

    private const string ConfigPath =
        @"C:\ProgramData\AHTAgent\config.json";

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LoadVendorsButton_Click(
        object sender, RoutedEventArgs e)
    {
        await LoadVendorsAsync();
    }

    private async Task LoadVendorsAsync()
    {
        string? url = Environment.GetEnvironmentVariable(
            "AHT_VENDOR_LOOKUP_URL",
            EnvironmentVariableTarget.User);

        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            MessageBox.Show(
                "Configure a valid HTTPS machine environment variable " +
                "named AHT_VENDOR_LOOKUP_URL.",
                "Vendor URL missing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _loading = true;
            LoadVendorsButton.IsEnabled = false;
            InstallButton.IsEnabled = false;
            VendorCombo.IsEnabled = false;
            VendorCombo.ItemsSource = null;
            StatusText.Text = "Loading vendors...";

            using var response = await _httpClient.PostAsync(
                uri,
                new StringContent("{}", Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            JsonElement root = doc.RootElement;
var records = new List<JsonElement>();

if (root.ValueKind == JsonValueKind.Array)
{
    records.AddRange(root.EnumerateArray());
}
else if (root.ValueKind == JsonValueKind.Object &&
         root.TryGetProperty("value", out var value) &&
         value.ValueKind == JsonValueKind.Array)
{
    records.AddRange(value.EnumerateArray());
}
else if (root.ValueKind == JsonValueKind.Object &&
         (root.TryGetProperty("VendorId", out _) ||
          root.TryGetProperty("VendorID", out _) ||
          root.TryGetProperty("vendorId", out _)))
{
    // The current flow returns a single vendor object.
    records.Add(root);
}
else
{
    throw new InvalidDataException(
        "Unexpected vendor response. Expected a vendor object or array.");
}

var vendors = new List<VendorOption>();

foreach (var item in records)
{
    string? id = ReadField(item, "VendorId")
              ?? ReadField(item, "VendorID")
              ?? ReadField(item, "vendorId");

    string? name = ReadField(item, "VendorName")
                ?? ReadField(item, "vendorName")
                ?? ReadField(item, "Title")
                ?? ReadField(item, "title");

    string? status = ReadField(item, "Status")
                  ?? ReadField(item, "status");

    if (string.IsNullOrWhiteSpace(id) ||
        string.IsNullOrWhiteSpace(name))
    {
        continue;
    }

    if (!string.Equals(
        status, "Active",
        StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    vendors.Add(new VendorOption
    {
        VendorId = id,
        VendorName = name
    });
}

            VendorCombo.ItemsSource = vendors
    .OrderBy(v => v.VendorName)
    .ToList();

VendorCombo.IsEnabled = vendors.Count > 0;
VendorCombo.SelectedIndex = -1;

StatusText.Text = vendors.Count > 0
    ? $"Loaded {vendors.Count} active vendors. Select one to continue."
    : "No active vendors found.";

UpdateInstallButton();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to load vendors.";

            MessageBox.Show(
                ex.Message,
                "Vendor lookup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loading = false;
            LoadVendorsButton.IsEnabled = true;
            UpdateInstallButton();
        }
    }

    private static string? ReadField(
        JsonElement item, string name)
    {
        if (item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
            return property.GetString();

        if (property.ValueKind == JsonValueKind.Object &&
            property.TryGetProperty("Value", out var choice) &&
            choice.ValueKind == JsonValueKind.String)
        {
            return choice.GetString();
        }

        return null;
    }

    private void VendorCombo_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        UpdateInstallButton();
    }

    private void UpdateInstallButton()
{
    bool vendorSelected = VendorCombo.SelectedItem is VendorOption;

    InstallButton.IsEnabled = !_loading && vendorSelected;

    if (_loading)
    {
        StatusText.Text = "Loading vendors...";
    }
    else if (vendorSelected)
    {
        var vendor = (VendorOption)VendorCombo.SelectedItem;
        StatusText.Text = $"Selected: {vendor.VendorName}. Ready to install.";
    }
    else if (VendorCombo.Items.Count > 0)
    {
        StatusText.Text = "Select a vendor to continue.";
    }
    else
    {
        StatusText.Text = "Load the vendor list to continue.";
    }
}
    private void InstallButton_Click(
        object sender, RoutedEventArgs e)
    {
        if (VendorCombo.SelectedItem is not VendorOption vendor)
        {
            MessageBox.Show("Select a vendor before installing.");
            return;
        }

        if (!File.Exists(Path.Combine(
            PayloadPath, "AssetManagerAgent.exe")))
        {
            MessageBox.Show(
                $"Agent executable not found in:\n{PayloadPath}");
            return;
        }

        if (!IsAdministrator())
        {
            MessageBox.Show(
                "Close the installer and reopen it using Run as administrator.");
            return;
        }

        if (MessageBox.Show(
            $"Install for {vendor.VendorName} ({vendor.VendorId})?",
            "Confirm installation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            InstallButton.IsEnabled = false;
            LoadVendorsButton.IsEnabled = false;
            StatusText.Text = "Installing agent...";

            Directory.CreateDirectory(InstallPath);
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            JsonObject config = File.Exists(ConfigPath)
                ? JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject
                    ?? new JsonObject()
                : new JsonObject();

            config["VendorID"] = vendor.VendorId;
            config["VendorName"] = vendor.VendorName;

            string? apiUrl = Environment.GetEnvironmentVariable(
                "ASSET_MANAGER_API_URL",
                EnvironmentVariableTarget.Machine);

            if (!string.IsNullOrWhiteSpace(apiUrl))
                config["InventoryApiUrl"] = apiUrl;

            // Save configuration before installing files.
            File.WriteAllText(
                ConfigPath,
                config.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

            foreach (string file in Directory.GetFiles(
    PayloadPath,
    "*",
    SearchOption.AllDirectories))
{
    string relativePath = Path.GetRelativePath(PayloadPath, file);
    string firstDirectory = relativePath
        .Split(Path.DirectorySeparatorChar)[0];

    // Do not install the build-time data snapshot.
    if (string.Equals(
        firstDirectory,
        "data",
        StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    string destination = Path.Combine(InstallPath, relativePath);
    string? destinationDirectory = Path.GetDirectoryName(destination);

    if (!string.IsNullOrWhiteSpace(destinationDirectory))
    {
        Directory.CreateDirectory(destinationDirectory);
    }

    File.Copy(file, destination, overwrite: true);
}

            StatusText.Text = "Installation files copied successfully.";

            MessageBox.Show(
    $"Agent files installed successfully.\n\n" +
    $"Vendor: {vendor.VendorName}\n" +
    $"Vendor ID: {vendor.VendorId}\n\n" +
    $"Location: {InstallPath}\n" +
    $"Configuration: {ConfigPath}",
    "Installation complete",
    MessageBoxButton.OK,
    MessageBoxImage.Information);

Close();
return;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Installation failed.";
            MessageBox.Show(ex.Message, "Installation error");
        }
        finally
        {
            LoadVendorsButton.IsEnabled = true;
            UpdateInstallButton();
        }
    }

    private static bool IsAdministrator()
    {
        using var identity =
            System.Security.Principal.WindowsIdentity.GetCurrent();

        var principal =
            new System.Security.Principal.WindowsPrincipal(identity);

        return principal.IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private sealed class VendorOption
    {
        public string VendorId { get; init; } = "";
        public string VendorName { get; init; } = "";
        public string DisplayName => VendorName;
    }
}
