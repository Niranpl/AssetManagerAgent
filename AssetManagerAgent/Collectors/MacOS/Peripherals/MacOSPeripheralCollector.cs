using System.Text.RegularExpressions;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.MacOS.Peripherals;

public sealed class MacOSPeripheralCollector
{
    public List<PeripheralInformation> Collect(
    string hidOutput,
    string bluetoothOutput,
    string usbOutput)
    {
        var peripherals = new List<PeripheralInformation>();

        // HID devices
        if (!string.IsNullOrWhiteSpace(hidOutput))
        {
            var products = ExtractPhysicalHidProducts(hidOutput);

            foreach (var product in products)
            {
                if (!IsMeaningfulPeripheral(product))
                    continue;

                peripherals.Add(
                    new PeripheralInformation
                    {
                        Manufacturer = "Apple",
                        Model = product,
                        ConnectionType = "HID",
                        HardwareIdentifier =
                            $"HID|Apple|{product}",
                        IdentitySource = "ProductIdentity"
                    });
            }
        }

        // Bluetooth devices
        if (!string.IsNullOrWhiteSpace(bluetoothOutput))
        {
            peripherals.AddRange(
                ParseBluetoothDevices(bluetoothOutput));
        }
        // USB devices

if (!string.IsNullOrWhiteSpace(usbOutput))
{
    peripherals.AddRange(
        ParseUsbDevices(usbOutput));
}

        // Prefer Bluetooth identity over generic HID identity.
// Bluetooth devices have a stronger physical identity (Bluetooth address).
var bluetoothModels = peripherals
    .Where(x =>
        x.ConnectionType.StartsWith(
            "Bluetooth",
            StringComparison.OrdinalIgnoreCase))
    .Select(x => x.Model)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

return peripherals
    .Where(x =>
        !string.IsNullOrWhiteSpace(
            x.HardwareIdentifier))
    .Where(x =>
        !x.ConnectionType.Equals("HID",
            StringComparison.OrdinalIgnoreCase)
        || !bluetoothModels.Contains(x.Model))
    .GroupBy(
        x => x.HardwareIdentifier,
        StringComparer.OrdinalIgnoreCase)
    .Select(x => x.First())
    .ToList();
    }
private static List<string> ExtractPhysicalHidProducts(
    string output)
{
    var products = new List<string>();

    var matches = Regex.Matches(
        output,
        @"(?s)""Product""\s*=\s*""([^""]+)""(?:(?!""Product""\s*=).)*?""Transport""\s*=\s*""([^""]+)""",
        RegexOptions.IgnoreCase);

    foreach (Match match in matches)
    {
        var product = match.Groups[1].Value.Trim();
        var transport = match.Groups[2].Value.Trim();

        if (string.IsNullOrWhiteSpace(product))
            continue;

        // Ignore macOS internal audio HID devices.
        if (transport.Equals(
                "Audio",
                StringComparison.OrdinalIgnoreCase))
            continue;

        // Ignore Apple internal/system HID devices.
        if (product.Equals(
                "Headset",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (product.Equals(
                "Keyboard Backlight",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (product.StartsWith(
                "PMU",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (product.StartsWith(
                "NAND",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (product.StartsWith(
                "gas gauge",
                StringComparison.OrdinalIgnoreCase))
            continue;

        products.Add(product);
    }

    return products
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
    private static List<string> ExtractProducts(
        string output)
    {
        var products = new List<string>();

        var matches = Regex.Matches(
            output,
            @"""Product""\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var product =
                match.Groups[1].Value.Trim();

            if (!string.IsNullOrWhiteSpace(product))
                products.Add(product);
        }

        return products;
    }

    private static List<PeripheralInformation>
        ParseBluetoothDevices(string output)
    {
        var peripherals =
            new List<PeripheralInformation>();

        var connectedIndex =
            output.IndexOf(
                "Connected:",
                StringComparison.OrdinalIgnoreCase);

        if (connectedIndex < 0)
            return peripherals;

        var connectedSection =
            output[connectedIndex..];

        var deviceMatches =
            Regex.Matches(
                connectedSection,
                @"(?m)^\s{10}(.+?):\s*$");

        foreach (Match match in deviceMatches)
        {
            var model =
                match.Groups[1].Value.Trim();

            if (string.IsNullOrWhiteSpace(model))
                continue;

            var blockStart =
                match.Index + match.Length;

            var nextDevice =
                Regex.Match(
                    connectedSection[blockStart..],
                    @"(?m)^\s{10}.+?:\s*$");

            var blockLength =
                nextDevice.Success
                    ? nextDevice.Index
                    : connectedSection.Length - blockStart;

            var block =
                connectedSection.Substring(
                    blockStart,
                    blockLength);

            var address =
                ExtractValue(
                    block,
                    "Address:");

            var minorType =
                ExtractValue(
                    block,
                    "Minor Type:");

            var manufacturer = "Bluetooth";

            var hardwareIdentifier =
                !string.IsNullOrWhiteSpace(address)
                    ? $"BT|{address.ToUpperInvariant()}"
                    : $"BT|{model}";

            peripherals.Add(
                new PeripheralInformation
                {
                    Manufacturer = manufacturer,

                    Model = model,

                    ConnectionType =
                        !string.IsNullOrWhiteSpace(minorType)
                            ? $"Bluetooth {minorType}"
                            : "Bluetooth",

                    SerialNumber = string.Empty,

                    HardwareIdentifier =
                        hardwareIdentifier,

                    IdentitySource =
                        !string.IsNullOrWhiteSpace(address)
                            ? "BluetoothAddress"
                            : "BluetoothProduct"
                });
        }

        return peripherals;
    }
    private static List<PeripheralInformation>
    ParseUsbDevices(string output)
{
    var peripherals =
        new List<PeripheralInformation>();

    if (string.IsNullOrWhiteSpace(output))
        return peripherals;

    // system_profiler SPUSBDataType is hierarchical text.
    // Look for USB device entries that expose Product ID / Vendor ID.
    var blocks =
        Regex.Split(
            output,
            @"(?m)^\s{4,}(?=[^\s].*:)"
        );

    foreach (var block in blocks)
    {
        if (string.IsNullOrWhiteSpace(block))
            continue;

        var product =
            ExtractValue(block, "Product ID:");

        var vendor =
            ExtractValue(block, "Vendor ID:");

        var serial =
            ExtractValue(block, "Serial Number:");

        if (string.IsNullOrWhiteSpace(product) &&
            string.IsNullOrWhiteSpace(vendor))
            continue;

        var name =
            ExtractUsbDeviceName(block);

        if (string.IsNullOrWhiteSpace(name))
            name = "USB Device";

        var hardwareIdentifier =
            !string.IsNullOrWhiteSpace(serial)
                ? $"USB|SERIAL|{serial}"
                : !string.IsNullOrWhiteSpace(vendor) &&
                  !string.IsNullOrWhiteSpace(product)
                    ? $"USB|{vendor}|{product}|{name}"
                    : $"USB|{name}";

        peripherals.Add(
            new PeripheralInformation
            {
                Manufacturer =
                    CleanUsbValue(vendor),

                Model = name,

                SerialNumber =
                    CleanUsbValue(serial),

                ConnectionType = "USB",

                HardwareIdentifier =
                    hardwareIdentifier,

                IdentitySource =
                    !string.IsNullOrWhiteSpace(serial)
                        ? "USBSerial"
                        : "USBDeviceIdentity"
            });
    }

    return peripherals
        .Where(x =>
            !string.IsNullOrWhiteSpace(
                x.HardwareIdentifier))
        .GroupBy(
            x => x.HardwareIdentifier,
            StringComparer.OrdinalIgnoreCase)
        .Select(x => x.First())
        .ToList();
}

private static string ExtractUsbDeviceName(string block)
{
    var lines =
        block.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);

    foreach (var line in lines)
    {
        var trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            continue;

        if (trimmed.Contains("Product ID:",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (trimmed.Contains("Vendor ID:",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (trimmed.Contains("Serial Number:",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (trimmed.Contains("Speed:",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (trimmed.Contains("Location ID:",
                StringComparison.OrdinalIgnoreCase))
            continue;

        if (trimmed.EndsWith(":"))
            return trimmed.TrimEnd(':').Trim();
    }

    return string.Empty;
}

private static string CleanUsbValue(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    return value
        .Replace("(0x", " ")
        .Replace(")", "")
        .Trim();
}

    private static string ExtractValue(
        string block,
        string key)
    {
        var match =
            Regex.Match(
                block,
                Regex.Escape(key) +
                @"\s*(.+)",
                RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups[1].Value.Trim()
            : string.Empty;
    }

    private static bool IsMeaningfulPeripheral(
        string product)
    {
        var value = product.Trim();

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var ignored = new[]
        {
            "NAND",
            "PMU",
            "Thermal",
            "Temperature",
            "Gas Gauge",
            "Keyboard Backlight",
            "Ambient",
            "Sensor",
            "Power Source",
            "IOHIDSystem",
            "AppleUserHIDEventDriver"
        };

        if (ignored.Any(
                x => value.Contains(
                    x,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var recognized = new[]
        {
            "Keyboard",
            "Trackpad",
            "Mouse",
            "Headset",
            "Touch",
            "Gamepad",
            "Joystick",
            "Digitizer",
            "Tablet"
        };

        return recognized.Any(
            x => value.Contains(
                x,
                StringComparison.OrdinalIgnoreCase));
    }
}