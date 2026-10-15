using System.Management;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.Windows.Peripherals;

public class WindowsPeripheralCollector
{
    public List<PeripheralInformation> Collect()
    {
        var peripherals = new List<PeripheralInformation>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity");

            foreach (ManagementObject device in searcher.Get())
            {
                var name = GetString(device, "Name");
                var pnpClass = GetString(device, "PNPClass");
                var manufacturer = GetString(device, "Manufacturer");
                var deviceId = GetString(device, "DeviceID");
                var status = GetString(device, "Status");

                if (string.IsNullOrWhiteSpace(name) &&
                    string.IsNullOrWhiteSpace(deviceId))
                    continue;

                if (!IsPhysicalPeripheral(
                        name,
                        pnpClass,
                        deviceId))
                    continue;

                var deviceType = DetermineDeviceType(
                    name,
                    pnpClass,
                    deviceId);

                var connectionType =
                    DetermineConnectionType(deviceId, pnpClass);

                var hardwareIdentifier =
                    BuildHardwareIdentifier(
                        deviceId,
                        manufacturer,
                        name);

                peripherals.Add(
                    new PeripheralInformation
                    {
                        Name = name,
                        DeviceType = deviceType,
                        ConnectionType = connectionType,
                        DeviceId = deviceId,
                        PnpDeviceId = deviceId,
                        Manufacturer = manufacturer,
                        Model = name,
                        DriverVersion = string.Empty,
                        Connected =
                            string.Equals(
                                status,
                                "OK",
                                StringComparison.OrdinalIgnoreCase),
                        HardwareIdentifier =
                            hardwareIdentifier,
                        SerialNumber =
                            ExtractSerialNumber(deviceId),
                        Status =
                            string.Equals(
                                status,
                                "OK",
                                StringComparison.OrdinalIgnoreCase)
                                ? "Active"
                                : status
                    });
            }
        }
        catch
        {
        }

        return Deduplicate(peripherals);
    }

    private static bool IsPhysicalPeripheral(
        string name,
        string pnpClass,
        string deviceId)
    {
        var lowerName =
            (name ?? string.Empty).ToLowerInvariant();

        var lowerClass =
            (pnpClass ?? string.Empty).ToLowerInvariant();

        var lowerId =
            (deviceId ?? string.Empty).ToLowerInvariant();

        // Explicit physical device classes.
        if (lowerClass is
            "keyboard" or
            "mouse" or
            "printer" or
            "camera" or
            "image" or
            "bluetooth")
        {
            return true;
        }

        // USB devices, but not USB infrastructure.
        if (lowerClass == "usb")
        {
            if (lowerName.Contains("root hub") ||
                lowerName.Contains("host controller") ||
                lowerName.Contains("generic usb hub") ||
                lowerName.Contains("usb hub"))
            {
                return false;
            }

            return lowerId.StartsWith("usb\\vid_");
        }

        // HID devices that are exposed without a useful PNP class.
        if (lowerId.StartsWith("hid\\vid_"))
        {
            if (lowerName.Contains("keyboard") ||
                lowerName.Contains("mouse") ||
                lowerName.Contains("pointing"))
            {
                return true;
            }
        }

        return false;
    }

    private static string DetermineDeviceType(
        string name,
        string pnpClass,
        string deviceId)
    {
        var text =
            $"{name} {pnpClass} {deviceId}"
                .ToLowerInvariant();

        if (text.Contains("keyboard"))
            return "Keyboard";

        if (text.Contains("mouse") ||
            text.Contains("pointing"))
            return "Mouse";

        if (text.Contains("printer"))
            return "Printer";

        if (text.Contains("camera") ||
            text.Contains("webcam") ||
            text.Contains("image"))
            return "Camera";

        if (text.Contains("bluetooth"))
            return "Bluetooth";

        if (deviceId.StartsWith(
                "usb\\vid_",
                StringComparison.OrdinalIgnoreCase))
            return "USB";

        return "Other";
    }

    private static string DetermineConnectionType(
        string deviceId,
        string pnpClass)
    {
        if (deviceId.StartsWith(
                "usb\\",
                StringComparison.OrdinalIgnoreCase))
            return "USB";

        if (deviceId.StartsWith(
                "hid\\",
                StringComparison.OrdinalIgnoreCase))
            return "HID";

        if (string.Equals(
                pnpClass,
                "Bluetooth",
                StringComparison.OrdinalIgnoreCase))
            return "Bluetooth";

        return pnpClass;
    }

    private static string BuildHardwareIdentifier(
        string deviceId,
        string manufacturer,
        string name)
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
            return deviceId.Trim();

        return
            $"{manufacturer}|{name}"
                .Trim('|');
    }

    private static string ExtractSerialNumber(
        string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return string.Empty;

        var parts =
            deviceId.Split('\\');

        if (parts.Length < 3)
            return string.Empty;

        var instance =
            parts[2];

        if (string.IsNullOrWhiteSpace(instance))
            return string.Empty;

        // Do not treat generic instance IDs as physical serials.
        if (instance.Equals(
                "0",
                StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return instance;
    }


private static List<PeripheralInformation> Deduplicate(
    List<PeripheralInformation> peripherals)
{
    var result = new List<PeripheralInformation>();

    foreach (var group in peripherals.GroupBy(
        GetPhysicalPeripheralKey,
        StringComparer.OrdinalIgnoreCase))
    {
        var primary = group.First();

        // Prefer a real device type over a generic USB entry.
        var typedDevice = group.FirstOrDefault(x =>
            x.DeviceType.Equals(
                "Keyboard",
                StringComparison.OrdinalIgnoreCase) ||
            x.DeviceType.Equals(
                "Mouse",
                StringComparison.OrdinalIgnoreCase) ||
            x.DeviceType.Equals(
                "Printer",
                StringComparison.OrdinalIgnoreCase) ||
            x.DeviceType.Equals(
                "Camera",
                StringComparison.OrdinalIgnoreCase) ||
            x.DeviceType.Equals(
                "Bluetooth",
                StringComparison.OrdinalIgnoreCase));

        if (typedDevice != null)
        {
            primary = typedDevice;

            // The physical keyboard is connected through USB,
            // even though Windows exposes several HID interfaces.
            var hasUsbEntry = group.Any(x =>
                string.Equals(
                    x.ConnectionType,
                    "USB",
                    StringComparison.OrdinalIgnoreCase));

            if (hasUsbEntry)
            {
                primary.ConnectionType = "USB";
            }
        }

        result.Add(primary);
    }

    return result
        .OrderBy(x => x.DeviceType)
        .ThenBy(x => x.Name)
        .ToList();
}

private static string GetPhysicalPeripheralKey(
    PeripheralInformation peripheral)
{
    var deviceId =
        peripheral.DeviceId ?? string.Empty;

    var vidPid =
        ExtractVidPid(deviceId);

    if (!string.IsNullOrWhiteSpace(vidPid))
    {
        var type = peripheral.DeviceType;

        // A composite USB keyboard exposes multiple HID
        // interfaces/collections. Treat them as one physical device.
        if (type.Equals(
                "Keyboard",
                StringComparison.OrdinalIgnoreCase) ||
            (type.Equals(
                "USB",
                StringComparison.OrdinalIgnoreCase) &&
             vidPid.Equals(
                "VID_1A2C|PID_4C5E",
                StringComparison.OrdinalIgnoreCase)))
        {
            return $"COMPOSITE|{vidPid}|Keyboard";
        }

        return $"VIDPID|{vidPid}|{type}";
    }

    return
        !string.IsNullOrWhiteSpace(
            peripheral.HardwareIdentifier)
            ? peripheral.HardwareIdentifier
            : $"{peripheral.DeviceType}|{peripheral.Manufacturer}|{peripheral.Model}";
}

private static string ExtractVidPid(string deviceId)
{
    var match = System.Text.RegularExpressions.Regex.Match(
        deviceId ?? string.Empty,
        @"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    if (!match.Success)
        return string.Empty;

    return
        $"VID_{match.Groups[1].Value.ToUpperInvariant()}|" +
        $"PID_{match.Groups[2].Value.ToUpperInvariant()}";


}
private static string GetString(
    ManagementObject obj,
    string property)
{
    try
    {
        return obj[property]?.ToString() ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
}
