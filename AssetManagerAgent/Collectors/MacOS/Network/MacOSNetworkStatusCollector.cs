using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.MacOS.Network;

public sealed class MacOSNetworkStatusCollector
{
    public List<NetworkAdapter> Collect(
        string systemProfilerOutput,
        string hardwarePortsOutput)
    {
        var adapters = new List<NetworkAdapter>();

        var hardwareMacs =
            ParseHardwareMacAddresses(systemProfilerOutput);

        var hardwarePorts =
            ParseHardwarePorts(hardwarePortsOutput);

        foreach (var networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            var interfaceName = networkInterface.Name;

            // Keep physical-style macOS hardware interfaces.
            // Exclude virtual/system interfaces.
            if (!interfaceName.StartsWith(
                    "en",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var properties =
                networkInterface.GetIPProperties();

            var addresses =
                properties.UnicastAddresses;

            var ipv4 =
                addresses
                    .FirstOrDefault(x =>
                        x.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address
                    .ToString() ?? string.Empty;

            var ipv6 =
                addresses
                    .Where(x =>
                        x.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetworkV6)
                    .Select(x => x.Address.ToString())
                    .ToList();

            // Prefer the hardware MAC reported by macOS
            // system_profiler.
            var mac =
                hardwareMacs.TryGetValue(
                    interfaceName,
                    out var hardwareMac)
                    ? hardwareMac
                    : FormatMac(
                        networkInterface
                            .GetPhysicalAddress()
                            .ToString());

            var connected =
                networkInterface.OperationalStatus ==
                OperationalStatus.Up;

            adapters.Add(
    new NetworkAdapter
    {
        Interface = interfaceName,

        Name =
            hardwarePorts.TryGetValue(
                interfaceName,
                out var portName)
                ? portName
                : networkInterface.Description,

        Type =
            networkInterface.NetworkInterfaceType
                .ToString(),

        MacAddress = mac,

        IPv4Address = ipv4,

        IPv6Addresses = ipv6,

        Connected = connected,

        Manufacturer = string.Empty,

        Model =
            networkInterface.NetworkInterfaceType
                .ToString(),

        HardwareIdentifier =
            !string.IsNullOrWhiteSpace(mac)
                ? mac
                : interfaceName
    });
        }

        return adapters;
    }

    private static Dictionary<string, string>
        ParseHardwareMacAddresses(
            string output)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(output))
            return result;

        var blocks =
            Regex.Split(
                output,
                @"(?=^\s{4}\S.*\:$)",
                RegexOptions.Multiline);

        foreach (var block in blocks)
        {
            var match =
                Regex.Match(
                    block,
                    @"BSD Device Name:\s*(\S+)",
                    RegexOptions.IgnoreCase);

            if (!match.Success)
                continue;

            var interfaceName =
                match.Groups[1].Value.Trim();

            var macMatch =
                Regex.Match(
                    block,
                    @"MAC Address:\s*([0-9A-Fa-f:]{17})",
                    RegexOptions.IgnoreCase);

            if (!macMatch.Success)
                continue;

            result[interfaceName] =
                macMatch.Groups[1].Value
                    .ToUpperInvariant();
        }

        return result;
    }

    private static string FormatMac(string value)
    {
        if (value.Length != 12)
            return value;

        return string.Join(
            ":",
            Enumerable.Range(0, 6)
                .Select(i =>
                    value.Substring(i * 2, 2)));
    }
    private static Dictionary<string, string>
    ParseHardwarePorts(string output)
{
    var result =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(output))
        return result;

    string? hardwarePort = null;
    string? device = null;

    foreach (var rawLine in output.Split('\n'))
    {
        var line = rawLine.Trim();

        if (line.StartsWith(
                "Hardware Port:",
                StringComparison.OrdinalIgnoreCase))
        {
            hardwarePort =
                line["Hardware Port:".Length..].Trim();

            device = null;
            continue;
        }

        if (line.StartsWith(
                "Device:",
                StringComparison.OrdinalIgnoreCase))
        {
            device =
                line["Device:".Length..].Trim();

            if (!string.IsNullOrWhiteSpace(device) &&
                !string.IsNullOrWhiteSpace(hardwarePort))
            {
                result[device] = hardwarePort;
            }
        }
    }

    return result;
}
}
