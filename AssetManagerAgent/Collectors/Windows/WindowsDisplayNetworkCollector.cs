using System.Management;
using AssetManagerAgent.Core.Models;
using System.Net.NetworkInformation;

namespace AssetManagerAgent.Collectors.Windows;

public class WindowsDisplayNetworkCollector
{
    public List<GpuInformation> CollectGpus()
    {
        var result = new List<GpuInformation>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_VideoController");

        foreach (ManagementObject gpu in searcher.Get())
        {
            var name = GetString(gpu, "Name");
            var manufacturer = GetString(gpu, "AdapterCompatibility");
            var deviceId = GetString(gpu, "PNPDeviceID");
            var driverVersion = GetString(gpu, "DriverVersion");

            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new GpuInformation
            {
                Name = name,
                Manufacturer = manufacturer,
                Model = name,
                HardwareIdentifier = !string.IsNullOrWhiteSpace(deviceId)
                    ? deviceId
                    : $"GPU|{manufacturer}|{name}",

                VendorId = GetVendorId(deviceId),
                Bus = GetString(gpu, "VideoProcessor"),
                Type = "GPU",
DriverVersion = driverVersion,
                MemoryGB = Math.Round(
                    GetLong(gpu, "AdapterRAM") /
                    1024d / 1024d / 1024d, 2)
            });
        }

        return result;
    }

    public List<MonitorInformation> CollectMonitors()
    {
        var monitors = new List<MonitorInformation>();

        using var searcher = new ManagementObjectSearcher(
            @"root\WMI",
            "SELECT * FROM WmiMonitorID");

        foreach (ManagementObject monitor in searcher.Get())
        {
            var manufacturer = DecodeStringArray(
                monitor["ManufacturerName"]);

            var product = DecodeStringArray(
                monitor["UserFriendlyName"]);

            var serial = DecodeStringArray(
                monitor["SerialNumberID"]);

            var instance = GetString(
                monitor,
                "InstanceName");

            var name = !string.IsNullOrWhiteSpace(product)
                ? product
                : manufacturer;

            if (string.IsNullOrWhiteSpace(name))
                name = "Monitor";

            monitors.Add(new MonitorInformation
            {
                Name = name,
                Manufacturer = manufacturer,
                Model = product,
                SerialNumber = serial,
                HardwareIdentifier =
                    !string.IsNullOrWhiteSpace(serial)
                        ? $"EDID|{serial}"
                        : $"MONITOR|{manufacturer}|{product}|{instance}",

                Internal = IsInternalMonitor(instance),
                Online = GetBool(
                    monitor,
                    "Active")
            });
        }

        return monitors;
    }

    public List<NetworkAdapter> CollectNetworkAdapters()
{
    var adapters = new List<NetworkAdapter>();

    try
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            // Ignore interfaces that are not actual physical/network hardware.
            if (networkInterface.NetworkInterfaceType is
                NetworkInterfaceType.Loopback or
                NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var name = networkInterface.Name ?? string.Empty;
            var description = networkInterface.Description ?? string.Empty;

            if (!IsWindowsNetworkAdapterPresent(networkInterface.Id))
{
    continue;
}

//             // Ignore network adapters whose physical PnP device is no longer present.
// if (!IsPhysicalNetworkAdapterPresent(networkInterface.Id))
// {
//     continue;
// }

            // Ignore Windows filter, QoS and lightweight filter interfaces.
            if (name.Contains("-WFP", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("-QoS", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("WFP", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Ignore WAN Miniport interfaces.
            if (name.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Ignore Microsoft virtual networking interfaces.
            if (description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("6to4", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("IP-HTTPS", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var physicalAddress =
                networkInterface.GetPhysicalAddress();

            var macAddress =
    physicalAddress != null
        ? string.Join(
            "",
            physicalAddress
                .GetAddressBytes()
                .Select(b => b.ToString("X2")))
        : string.Empty;

            var ipProperties =
                networkInterface.GetIPProperties();

            var ipv4 =
                ipProperties.UnicastAddresses
                    .Where(x =>
                        x.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(x => x.Address.ToString())
                    .FirstOrDefault() ?? string.Empty;

            var ipv6 =
                ipProperties.UnicastAddresses
                    .Where(x =>
                        x.Address.AddressFamily ==
                        System.Net.Sockets.AddressFamily.InterNetworkV6)
                    .Select(x => x.Address.ToString())
                    .ToList();

            var connected =
                networkInterface.OperationalStatus ==
                OperationalStatus.Up;

            var type =
                networkInterface.NetworkInterfaceType.ToString();

            var adapter = new NetworkAdapter
            {
                Interface = networkInterface.Id,
                Name = name,
                Type = type,
                MacAddress = macAddress,
                IPv4Address = ipv4,
                IPv6Addresses = ipv6,
                Connected = connected,

                Manufacturer = string.Empty,
                Model = description,
                SerialNumber = string.Empty,
                PartNumber = string.Empty,

                HardwareIdentifier =
                    !string.IsNullOrWhiteSpace(macAddress)
                        ? macAddress
                        : $"{networkInterface.Id}|{name}|{type}",

                IdentitySource =
                    !string.IsNullOrWhiteSpace(macAddress)
                        ? "MACAddress"
                        : "InterfaceIdentity"
            };

            adapters.Add(adapter);
        }
    }
    catch
    {
        // Inventory collection must continue even if
        // one network interface cannot be queried.
    }

    // Remove duplicate physical adapters.
    adapters = adapters
        .GroupBy(x =>
            !string.IsNullOrWhiteSpace(x.MacAddress)
                ? x.MacAddress
                : x.HardwareIdentifier,
            StringComparer.OrdinalIgnoreCase)
        .Select(g =>
            g.OrderByDescending(x => x.Connected)
             .ThenByDescending(x =>
                 !string.IsNullOrWhiteSpace(x.IPv4Address))
             .First())
        .OrderBy(x => x.Name)
        .ToList();

    return adapters;
}
    public NetworkAdapter? GetPrimaryNetworkAdapter(
    List<NetworkAdapter> adapters)
{
    return adapters
        .Where(x => x.Connected)
        .Where(x => !string.IsNullOrWhiteSpace(x.MacAddress))
        .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.IPv4Address))
        .FirstOrDefault();
}

private static bool IsWindowsNetworkAdapterPresent(string interfaceId)
{
    try
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT GUID FROM Win32_NetworkAdapter");

        foreach (ManagementObject adapter in searcher.Get())
        {
            var guid = adapter["GUID"]?.ToString();

            if (string.Equals(
                    NormalizeGuid(guid),
                    NormalizeGuid(interfaceId),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }
    catch
    {
        // If WMI verification fails, do not discard the adapter.
        return true;
    }

    return false;
}

private static string NormalizeGuid(string? value)
{
    return (value ?? string.Empty)
        .Trim()
        .Trim('{', '}');
}
    private static string GetVendorId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return string.Empty;

        var parts = deviceId.Split('\\');

        if (parts.Length == 0)
            return string.Empty;

        var value = parts[^1];

        var venIndex = value.IndexOf(
            "VEN_",
            StringComparison.OrdinalIgnoreCase);

        if (venIndex >= 0 &&
            value.Length >= venIndex + 8)
        {
            return value.Substring(
                venIndex,
                8);
        }

        return string.Empty;
    }

    private static bool IsInternalMonitor(
        string instance)
    {
        return instance.Contains(
            "DISPLAY\\",
            StringComparison.OrdinalIgnoreCase) &&
            instance.Contains(
                "INTERNAL",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string DecodeStringArray(
        object? value)
    {
        if (value is not ushort[] data)
            return string.Empty;

        return new string(
            data
                .Where(x => x != 0)
                .Select(x => (char)x)
                .ToArray())
            .Trim();
    }

    private static string GetString(
        ManagementBaseObject obj,
        string property)
    {
        try
        {
            return obj[property]
                ?.ToString()
                ?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static long GetLong(
        ManagementBaseObject obj,
        string property)
    {
        try
        {
            return Convert.ToInt64(
                obj[property]);
        }
        catch
        {
            return 0;
        }
    }

    private static bool GetBool(
        ManagementBaseObject obj,
        string property)
    {
        try
        {
            return Convert.ToBoolean(
                obj[property]);
        }
        catch
        {
            return false;
        }
    }
    private static string FormatMacAddress(
    PhysicalAddress address)
{
    if (address == null)
        return string.Empty;

    var bytes = address.GetAddressBytes();

    return string.Join(
        "",
        bytes.Select(b => b.ToString("X2")));
}
}
