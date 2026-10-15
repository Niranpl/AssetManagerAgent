    using AssetManagerAgent.Collectors.MacOS.Software;
    using AssetManagerAgent.Collectors.MacOS.Peripherals;
        using AssetManagerAgent.Core.Interfaces;
    using AssetManagerAgent.Core.Models;
    using AssetManagerAgent.Services.Platform;

    namespace AssetManagerAgent.Collectors.MacOS;

    public sealed class MacOSCollector : IPlatformCollector
    {
        private readonly CommandRunner _commandRunner;

        public MacOSCollector(CommandRunner commandRunner)
        {
            _commandRunner = commandRunner;
        }

        public string PlatformName => "macOS";

        public bool CanRun()
        {
            return OperatingSystem.IsMacOS();
        }

        public async Task<Asset> CollectAsync()
        {
            if (!CanRun())
                throw new PlatformNotSupportedException(
                    "MacOSCollector can only run on macOS.");

            var now = DateTime.UtcNow;

            var serial = await Run("ioreg", "-l");
            var systemVersion = await Run("sw_vers");
            var hardware = await Run("system_profiler", "SPHardwareDataType");
            var memory = await Run("system_profiler", "SPMemoryDataType");
            var sysctl = await Run("sysctl", "-a");
            var storage = await Run("system_profiler", "-json", "SPStorageDataType");
        var nvme = await Run(
        "system_profiler",
        "SPNVMeDataType");        
    var power = await Run("system_profiler", "SPPowerDataType");
            var displays = await Run("system_profiler", "SPDisplaysDataType");
            var network = await Run("system_profiler", "SPNetworkDataType");

        var software = await Run(
        "system_profiler",
        "-json",
        "SPApplicationsDataType");
            var asset = new Asset
            {
                FirstDetected = now,
                LastScan = now,

                SerialNumber = GetIoregValue(serial, "IOPlatformSerialNumber"),
                ComputerName = GetCommandValue(
                    await Run("scutil", "--get", "ComputerName")),

                Manufacturer = "Apple",

                Model = GetSystemProfilerValue(
                    hardware,
                    "Model Name"),

                ModelIdentifier = GetSystemProfilerValue(
                    hardware,
                    "Model Identifier"),

                OperatingSystem = GetCommandValue(
                    await Run("sw_vers", "-productName")),

                OperatingSystemVersion = GetCommandValue(
                    await Run("sw_vers", "-productVersion"))
            };

            asset.Cpu = ParseCpu(hardware, sysctl);
            asset.Memory = ParseMemory(memory);
                    var diskUtilInfo = await Run(
            "diskutil",
            "info",
            "disk0");

        asset.Storage = ParseStorage(
            storage,
            nvme,
            diskUtilInfo);
            asset.Batteries = ParseBattery(power);
            asset.Bios = ParseFirmware(hardware);
            asset.Motherboard = ParseMotherboard(hardware);
            ParseDisplaysAndGpu(displays, asset);
                
    var hardwarePorts =
    await Run(
        "networksetup",
        "-listallhardwareports");

asset.NetworkAdapters =
    new AssetManagerAgent.Collectors.MacOS.Network.MacOSNetworkStatusCollector()
        .Collect(
            network,
            hardwarePorts);
    asset.Software =
        new MacOSSoftwareCollector()
            .Collect(software);
    var hidDevices =
    await Run(
        "ioreg",
        "-l",
        "-w",
        "0",
        "-c",
        "IOHIDDevice");

var bluetoothDevices =
    await Run(
        "system_profiler",
        "SPBluetoothDataType");

        var usbDevices = await Run("system_profiler", "SPUSBDataType");

asset.Peripherals =
    new MacOSPeripheralCollector().Collect(
        hidDevices,
        bluetoothDevices,
        usbDevices);

            return asset;
        }

        private static List<NetworkAdapter> ParseNetwork(
            string output)
        {
            var adapters = new List<NetworkAdapter>();

            if (string.IsNullOrWhiteSpace(output))
                return adapters;

            NetworkAdapter? current = null;
            string section = string.Empty;

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line == "Network:")
                    continue;

                // A top-level adapter/service name has no indentation
                // after Trim(), but nested sections are identified
                // explicitly below.
                if (line.EndsWith(":") &&
                    !line.Contains(": "))
                {
                    var name = line.TrimEnd(':');

                    if (name is "IPv4" or
                        "IPv6" or
                        "Ethernet" or
                        "Proxies" or
                        "DNS" or
                        "DHCP Server Responses" or
                        "Additional Routes")
                    {
                        section = name;
                        continue;
                    }

                    if (name is "Media Options" or
                        "Media Subtype")
                    {
                        section = name;
                        continue;
                    }

                    // A new adapter/service.
                    if (current != null)
                        adapters.Add(current);

                    current = new NetworkAdapter
                    {
                        Name = name,
                        ComponentType = "NetworkAdapter"
                    };

                    section = string.Empty;
                    continue;
                }

                if (current == null)
                    continue;

                if (line.StartsWith("Type:"))
                {
                    current.Type =
                        GetValueAfterColon(line);
                }
                else if (line.StartsWith("Hardware:"))
                {
                    current.Model =
                        GetValueAfterColon(line);
                }
                else if (line.StartsWith("BSD Device Name:"))
                {
                    current.Interface =
                        GetValueAfterColon(line);
                }
                else if (line.StartsWith("IPv4 Addresses:"))
                {
                    current.IPv4Address =
                        GetValueAfterColon(line);
                }
                else if (section == "IPv4" &&
                        line.StartsWith("Addresses:"))
                {
                    current.IPv4Address =
                        GetValueAfterColon(line);
                }
                else if (section == "IPv6" &&
                        line.StartsWith("Addresses:"))
                {
                    var address =
                        GetValueAfterColon(line);

                    if (!string.IsNullOrWhiteSpace(address))
                        current.IPv6Addresses.Add(address);
                }
                else if (section == "Ethernet" &&
                        line.StartsWith("MAC Address:"))
                {
                    current.MacAddress =
                        GetValueAfterColon(line);
                }
            }

            if (current != null)
                adapters.Add(current);

            // Remove entries that are not real adapters.
            adapters = adapters
                .Where(a =>
                    !string.IsNullOrWhiteSpace(a.Interface) ||
                    !string.IsNullOrWhiteSpace(a.MacAddress))
                .ToList();

            return adapters;
        }

        private static void ParseDisplaysAndGpu(
    string output,
    Asset asset)
{
    if (string.IsNullOrWhiteSpace(output))
        return;

    GpuInformation? gpu = null;
    MonitorInformation? monitor = null;

    bool inDisplays = false;

    foreach (var rawLine in output.Split('\n'))
    {
        if (string.IsNullOrWhiteSpace(rawLine))
            continue;

        var line = rawLine.Trim();

        // ---------------------------------------------------------
        // GPU
        // ---------------------------------------------------------
        if (!inDisplays && line == "Displays:")
{
    if (gpu != null)
    {
        asset.Gpus.Add(gpu);
        gpu = null;
    }

    inDisplays = true;
    continue;
}
        if (!inDisplays &&
    line.EndsWith(":") &&
    !line.Contains(": ") &&
    !line.Equals(
        "Graphics/Displays:",
        StringComparison.OrdinalIgnoreCase))
        {
            if (gpu != null)
            {
                asset.Gpus.Add(gpu);
            }

            var gpuName = line.TrimEnd(':');

            gpu = new GpuInformation
            {
                Name = gpuName,
                Model = gpuName,
                ComponentType = "GPU"
            };

            continue;
        }

        if (!inDisplays && gpu != null)
        {
            if (line.StartsWith("Chipset Model:"))
            {
                gpu.Name = GetValueAfterColon(line);
                gpu.Model = gpu.Name;
            }
            else if (line.StartsWith("Vendor:"))
            {
                var vendor = GetValueAfterColon(line);

                var openParen = vendor.IndexOf('(');
                var closeParen = vendor.IndexOf(')');

                if (openParen >= 0)
                {
                    gpu.Manufacturer = vendor[..openParen].Trim();
                }
                else
                {
                    gpu.Manufacturer = vendor.Trim();
                }

                if (openParen >= 0 &&
                    closeParen > openParen)
                {
                    gpu.VendorId =
                        vendor[(openParen + 1)..closeParen].Trim();
                }
            }
            else if (line.StartsWith("Bus:"))
            {
                gpu.Bus = GetValueAfterColon(line);
            }
            else if (line.StartsWith("Type:"))
            {
                gpu.Type = GetValueAfterColon(line);
            }
            else if (line.StartsWith("Total Number of Cores:"))
            {
                var value = GetValueAfterColon(line);

                if (int.TryParse(value, out var cores))
                {
                    gpu.CoreCount = cores;
                }
            }
            else if (line.StartsWith("Metal Support:"))
            {
                gpu.MetalSupport = GetValueAfterColon(line);
            }
            else if (line == "Displays:")
            {
                // GPU is complete
                asset.Gpus.Add(gpu);
                gpu = null;

                inDisplays = true;
            }

            continue;
        }

        // ---------------------------------------------------------
        // DISPLAY / MONITOR
        // ---------------------------------------------------------

        if (inDisplays)
        {
            // Example:
            // Color LCD:

            if (line.EndsWith(":") &&
                !line.Contains(": "))
            {
                if (monitor != null)
                {
                    asset.Monitors.Add(monitor);
                }

                monitor = new MonitorInformation
                {
                    Name = line.TrimEnd(':'),
                    ComponentType = "Monitor"
                };

                continue;
            }

            if (monitor == null)
                continue;

            if (line.StartsWith("Display Type:"))
            {
                monitor.DisplayType =
                    GetValueAfterColon(line);

                monitor.Model =
                    monitor.DisplayType;
            }
            else if (line.StartsWith("Resolution:"))
            {
                monitor.Resolution =
                    GetValueAfterColon(line);
            }
            else if (line.StartsWith("Connection Type:"))
            {
                monitor.ConnectionType =
                    GetValueAfterColon(line);

                monitor.Internal =
                    monitor.ConnectionType.Equals(
                        "Internal",
                        StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("Main Display:"))
            {
                monitor.MainDisplay =
                    GetValueAfterColon(line).Equals(
                        "Yes",
                        StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("Online:"))
            {
                monitor.Online =
                    GetValueAfterColon(line).Equals(
                        "Yes",
                        StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ---------------------------------------------------------
    // Final GPU
    // ---------------------------------------------------------

    if (gpu != null)
    {
        asset.Gpus.Add(gpu);
    }

    // ---------------------------------------------------------
    // Final monitor
    // ---------------------------------------------------------

    if (monitor != null)
    {
        asset.Monitors.Add(monitor);
    }

    // ---------------------------------------------------------
    // Stable identities
    // ---------------------------------------------------------

    foreach (var g in asset.Gpus)
    {
        g.HardwareIdentifier =
            $"{g.Manufacturer}|{g.Model}|{g.VendorId}|{g.Bus}";
    }

    foreach (var m in asset.Monitors)
    {
        m.HardwareIdentifier =
            $"{m.Name}|{m.Model}|{m.Resolution}|{m.ConnectionType}";
    }
}

        private static BiosInformation ParseFirmware(
            string output)
        {
            return new BiosInformation
            {
                Manufacturer = "Apple",
                Version = GetSystemProfilerValue(
                    output,
                    "System Firmware Version"),
                ReleaseDate = string.Empty
            };
        }

        private static MotherboardInformation ParseMotherboard(
            string output)
        {
            var motherboard = new MotherboardInformation();

            if (string.IsNullOrWhiteSpace(output))
                return motherboard;

            var platformModel =
                GetSystemProfilerValue(
                    output,
                    "Model Identifier");

            if (!string.IsNullOrWhiteSpace(platformModel))
            {
                motherboard.HardwareIdentifier =
                    platformModel;
            }

            motherboard.BiosManufacturer = "Apple";

            motherboard.BiosVersion =
                GetSystemProfilerValue(
                    output,
                    "System Firmware Version");

            return motherboard;
        }

        private static List<BatteryInformation> ParseBattery(
            string output)
        {
            var batteries = new List<BatteryInformation>();

            if (string.IsNullOrWhiteSpace(output))
                return batteries;

            var battery = new BatteryInformation
            {
                ComponentType = "Battery"
            };

            var lines = output.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries);

            bool inModelInformation = false;
            bool inChargeInformation = false;
            bool inHealthInformation = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line == "Model Information:")
                {
                    inModelInformation = true;
                    inChargeInformation = false;
                    inHealthInformation = false;
                    continue;
                }

                if (line == "Charge Information:")
                {
                    inModelInformation = false;
                    inChargeInformation = true;
                    inHealthInformation = false;
                    continue;
                }

                if (line == "Health Information:")
                {
                    inModelInformation = false;
                    inChargeInformation = false;
                    inHealthInformation = true;
                    continue;
                }

                if (line == "Battery Power:")
                {
                    continue;
                }

                if (line.EndsWith(":") &&
                    !line.StartsWith("Serial Number:") &&
                    !line.StartsWith("Device Name:") &&
                    !line.StartsWith("Firmware Version:") &&
                    !line.StartsWith("Hardware Revision:") &&
                    !line.StartsWith("Cell Revision:") &&
                    !line.StartsWith("Pack Lot Code:") &&
                    !line.StartsWith("PCB Lot Code:"))
                {
                    if (line == "System Power Settings:" ||
                        line == "Hardware Configuration:" ||
                        line == "AC Charger Information:" ||
                        line == "Power Events:")
                    {
                    }
                }

                if (inModelInformation)
                {
                    if (line.StartsWith("Serial Number:"))
                    {
                        battery.SerialNumber =
                            GetValueAfterColon(line);
                    }
                    else if (line.StartsWith("Device Name:"))
                    {
                        battery.Model =
                            GetValueAfterColon(line);
                    }
                }

                if (inChargeInformation)
                {
                    if (line.StartsWith("Charging:"))
                    {
                        battery.Charging =
                            GetValueAfterColon(line)
                                .Equals(
                                    "Yes",
                                    StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("State of Charge (%):"))
                    {
                        battery.CurrentChargePercentage =
                            ParsePercentage(
                                GetValueAfterColon(line));
                    }
                }

                if (inHealthInformation)
                {
                    if (line.StartsWith("Cycle Count:"))
                    {
                        battery.CycleCount =
                            ParseInteger(
                                GetValueAfterColon(line));
                    }
                    else if (line.StartsWith("Condition:"))
                    {
                        battery.Condition =
                            GetValueAfterColon(line);
                    }
                    else if (line.StartsWith("Maximum Capacity:"))
                    {
                        battery.HealthPercentage =
                            ParsePercentage(
                                GetValueAfterColon(line));
                    }
                }

                if (line.StartsWith("Current Power Source:"))
                {
                    battery.PowerSource = "Battery";
                }
            }

            if (string.IsNullOrWhiteSpace(
                    battery.PowerSource))
            {
                battery.PowerSource =
                    battery.Charging
                        ? "AC"
                        : "Battery";
            }

            if (!string.IsNullOrWhiteSpace(
                    battery.SerialNumber) ||
                !string.IsNullOrWhiteSpace(
                    battery.Model))
            {
                batteries.Add(battery);
            }

            return batteries;
        }

        private static int ParsePercentage(
            string value)
        {
            var cleaned = value
                .Replace("%", "")
                .Trim();

            return int.TryParse(
                cleaned,
                out var result)
                ? result
                : 0;
        }

        private static int ParseInteger(
            string value)
        {
            var cleaned = value
                .Trim();

            return int.TryParse(
                cleaned,
                out var result)
                ? result
                : 0;
        }

        private static List<StorageDevice> ParseStorage(
            string output,
            string nvmeOutput,
            string diskUtilInfo)
        {
            var nvmeDevices =
    ParseNvmeDevices(nvmeOutput);

            var devices =
                new Dictionary<string, StorageDevice>(
                    StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(output))
                return [];

            try
            {
                using var document =
                    System.Text.Json.JsonDocument.Parse(output);

                if (!document.RootElement.TryGetProperty(
                        "SPStorageDataType",
                        out var storageArray))
                {
                    return [];
                }

                foreach (var volume in storageArray.EnumerateArray())
                {
                    if (!volume.TryGetProperty(
                            "physical_drive",
                            out var physicalDrive))
                    {
                        continue;
                    }

                    var deviceName =
                        GetJsonString(physicalDrive, "device_name");

                    var mediaName =
                        GetJsonString(physicalDrive, "media_name");

                    var protocol =
                        GetJsonString(physicalDrive, "protocol");

                    var internalDisk =
                        GetJsonString(physicalDrive, "is_internal_disk");

                    if (protocol.Equals(
                            "Disk Image",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!internalDisk.Equals(
                            "yes",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(deviceName))
                        continue;

                    if (!devices.TryGetValue(deviceName, out var device))
                    {
                        var mediumType =
                            GetJsonString(physicalDrive, "medium_type");

                        device = new StorageDevice
                        {
                            Name = !string.IsNullOrWhiteSpace(mediaName)
                                ? mediaName
                                : deviceName,
                            Device = deviceName,
                            PhysicalIdentifier = deviceName,
                            ComponentType = "DISK",
                            SerialNumber = TryGetNvmeDevice(
                            nvmeDevices,
                            mediaName,
                            deviceName)?.SerialNumber ?? string.Empty,

                        FirmwareRevision = TryGetNvmeDevice(
                            nvmeDevices,
                            mediaName,
                            deviceName)?.FirmwareRevision ?? string.Empty,

                        TrimSupported = TryGetNvmeDevice(
                            nvmeDevices,
                            mediaName,
                            deviceName)?.TrimSupported ?? false,
                            HardwareAesSupported = ParseDiskUtilBoolean(
                            diskUtilInfo,
                            "Hardware AES Support"),
                            Model = !string.IsNullOrWhiteSpace(mediaName)
                                ? mediaName
                                : deviceName,
                            MediaType = mediumType,
                            Protocol = protocol,
                            Internal = true,
                            SolidState = mediumType.Equals(
                                "ssd",
                                StringComparison.OrdinalIgnoreCase),
                            Virtual = false,
                            SmartStatus = GetJsonString(
                                physicalDrive,
                                "smart_status")
                        };

                        devices.Add(deviceName, device);
                    }

                    var capacityBytes = GetJsonInt64(
                        volume, "size_in_bytes");
                    var freeBytes = GetJsonInt64(
                        volume, "free_space_in_bytes");
                    var capacityGB = BytesToGB(capacityBytes);
                    var freeGB = BytesToGB(freeBytes);

                    var storageVolume = new StorageVolume
                    {
                        Name = GetJsonString(volume, "_name"),
                        MountPoint = GetJsonString(volume, "mount_point"),
                        FileSystem = GetJsonString(volume, "file_system"),
                        BsdName = GetJsonString(volume, "bsd_name"),
                        VolumeUuid = GetJsonString(volume, "volume_uuid"),
                        CapacityGB = capacityGB,
                        FreeGB = freeGB,
                        UsedGB = Math.Max(0, capacityGB - freeGB),
                        Writable = GetJsonString(volume, "writable").Equals(
                            "yes",
                            StringComparison.OrdinalIgnoreCase)
                    };

                    if (!device.Volumes.Any(existing =>
                            existing.VolumeUuid.Equals(
                                storageVolume.VolumeUuid,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        device.Volumes.Add(storageVolume);
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                return [];
            }

            foreach (var device in devices.Values)
            {
                if (device.Volumes.Count == 0)
                    continue;

                device.CapacityGB = device.Volumes.Max(
                    volume => volume.CapacityGB);
                device.FreeGB = device.Volumes.Min(
                    volume => volume.FreeGB);
                device.UsedGB = Math.Max(
                    0,
                    device.CapacityGB - device.FreeGB);
                device.FileSystem = string.Join(
                    ", ",
                    device.Volumes
                        .Select(volume => volume.FileSystem)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            return devices.Values.ToList();
        }

        private static NvmeDeviceInfo? TryGetNvmeDevice(
    Dictionary<string, NvmeDeviceInfo> nvmeDevices,
    string mediaName,
    string deviceName)
{
    if (!string.IsNullOrWhiteSpace(mediaName) &&
        nvmeDevices.TryGetValue(
            mediaName.Trim(),
            out var device))
    {
        return device;
    }

    if (!string.IsNullOrWhiteSpace(deviceName) &&
        nvmeDevices.TryGetValue(
            deviceName.Trim(),
            out device))
    {
        return device;
    }

    return null;
}
private static bool ParseDiskUtilBoolean(
    string output,
    string key)
{
    if (string.IsNullOrWhiteSpace(output))
        return false;

    foreach (var rawLine in output.Split('\n'))
    {
        var line = rawLine.Trim();

        var separatorIndex = line.IndexOf(':');

        if (separatorIndex < 0)
            continue;

        var actualKey = line[..separatorIndex].Trim();

        if (!actualKey.Equals(
                key,
                StringComparison.OrdinalIgnoreCase))
            continue;

        var value = line[(separatorIndex + 1)..].Trim();

        return value.Equals(
            "Yes",
            StringComparison.OrdinalIgnoreCase);
    }

    return false;
}
        private static string GetJsonString(
            System.Text.Json.JsonElement element,
            string property)
        {
            if (!element.TryGetProperty(
                property,
                out var value))
            {
                return string.Empty;
            }

            return value.ValueKind ==
                System.Text.Json.JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.ToString();
        }

private sealed class NvmeDeviceInfo
{
    public string SerialNumber { get; set; } = string.Empty;
    public string FirmwareRevision { get; set; } = string.Empty;
    public bool TrimSupported { get; set; }
}

private static Dictionary<string, NvmeDeviceInfo> ParseNvmeDevices(
    string output)
{
    var result =
        new Dictionary<string, NvmeDeviceInfo>(
            StringComparer.OrdinalIgnoreCase);

    if (string.IsNullOrWhiteSpace(output))
        return result;

    string? currentDevice = null;
    NvmeDeviceInfo? currentInfo = null;

    foreach (var rawLine in output.Split('\n'))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrWhiteSpace(line))
            continue;

        // Physical NVMe device header.
        if (line.EndsWith(":") &&
            !line.StartsWith(
                "NVMExpress",
                StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith(
                "Apple SSD Controller",
                StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith(
                "Volumes",
                StringComparison.OrdinalIgnoreCase))
        {
            if (currentDevice != null &&
                currentInfo != null)
            {
                result[currentDevice] = currentInfo;
            }

            currentDevice =
                line.TrimEnd(':').Trim();

            currentInfo = new NvmeDeviceInfo();

            continue;
        }

        if (currentInfo == null ||
            string.IsNullOrWhiteSpace(currentDevice))
        {
            continue;
        }

        if (line.StartsWith(
                "Serial Number:",
                StringComparison.OrdinalIgnoreCase))
        {
            currentInfo.SerialNumber =
                line["Serial Number:".Length..].Trim();
        }
        else if (line.StartsWith(
                     "Revision:",
                     StringComparison.OrdinalIgnoreCase))
        {
            currentInfo.FirmwareRevision =
                line["Revision:".Length..].Trim();
        }
        else if (line.StartsWith(
                     "TRIM Support:",
                     StringComparison.OrdinalIgnoreCase))
        {
            var value =
                line["TRIM Support:".Length..].Trim();

            currentInfo.TrimSupported =
                value.Equals(
                    "Yes",
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    if (currentDevice != null &&
        currentInfo != null)
    {
        result[currentDevice] = currentInfo;
    }

    return result;
}


        private static long GetJsonInt64(
            System.Text.Json.JsonElement element,
            string property)
        {
            if (!element.TryGetProperty(
                property,
                out var value))
            {
                return 0;
            }

            if (value.TryGetInt64(
                out var result))
            {
                return result;
            }

            return 0;
        }

        private static double BytesToGB(long bytes)
        {
            return bytes / 1_073_741_824d;
        }

        private static double ParseStorageSize(
            string value)
        {
            var cleaned = value
                .Replace(",", "")
                .Trim();

            var parts = cleaned.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 ||
                !double.TryParse(
                    parts[0],
                    out var number))
            {
                return 0;
            }

            if (parts.Length > 1)
            {
                var unit = parts[1].ToUpperInvariant();

                if (unit == "TB")
                    return number * 1024;

                if (unit == "MB")
                    return number / 1024;
            }

            return number;
        }

        private static CpuInformation ParseCpu(
            string hardware,
            string sysctl)
        {
            var cpu = new CpuInformation
            {
                Name = GetSystemProfilerValue(
                    hardware,
                    "Chip")
            };

            if (string.IsNullOrWhiteSpace(cpu.Name))
            {
                cpu.Name = GetSystemProfilerValue(
                    hardware,
                    "Processor Name");
            }

            cpu.Manufacturer = "Apple";

            var physicalCores = GetSystemProfilerValue(
                hardware,
                "Total Number of Cores");

            if (!string.IsNullOrWhiteSpace(physicalCores))
            {
                var coreNumber = physicalCores
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();

                if (int.TryParse(coreNumber, out var cores))
                {
                    cpu.PhysicalCores = cores;
                }
            }

            var logical = GetSysctlValue(
                sysctl,
                "hw.logicalcpu");

            if (int.TryParse(
                logical,
                out var logicalProcessors))
            {
                cpu.LogicalProcessors = logicalProcessors;
            }

            var frequency = GetSysctlValue(
                sysctl,
                "hw.cpufrequency_max");

            if (double.TryParse(
                frequency,
                out var hz) &&
                hz > 0)
            {
                cpu.MaxGHz = hz / 1_000_000_000;
            }

            return cpu;
        }

        private static List<MemoryModule> ParseMemory(
    string output)
{
    var modules = new List<MemoryModule>();

    if (string.IsNullOrWhiteSpace(output))
        return modules;

    var module = new MemoryModule
    {
        ComponentType = "RAM",
        Slot = "UnifiedMemory"
    };

    foreach (var rawLine in output.Split('\n'))
    {
        var line = rawLine.Trim();

        if (line.StartsWith("Memory:"))
        {
            module.CapacityGB =
                ParseMemorySize(GetValueAfterColon(line));
        }
        else if (line.StartsWith("Type:"))
        {
            module.MemoryType =
                GetValueAfterColon(line);
        }
        else if (line.StartsWith("Manufacturer:"))
        {
            module.Manufacturer =
                GetValueAfterColon(line);
        }
        else if (line.StartsWith("Serial Number:"))
        {
            module.SerialNumber =
                GetValueAfterColon(line);
        }
        else if (line.StartsWith("Part Number:"))
        {
            module.PartNumber =
                GetValueAfterColon(line);
        }
        else if (line.StartsWith("Speed:"))
        {
            module.SpeedMHz =
                ParseSpeed(GetValueAfterColon(line));
        }
    }

    if (module.CapacityGB <= 0 &&
        string.IsNullOrWhiteSpace(module.MemoryType) &&
        string.IsNullOrWhiteSpace(module.Manufacturer))
    {
        return modules;
    }

    // Apple Silicon unified memory normally has no DIMM serial.
    // Build a stable fallback identity from the information macOS exposes.
    if (!string.IsNullOrWhiteSpace(module.SerialNumber))
    {
        module.HardwareIdentifier =
            module.SerialNumber;
    }
    else if (!string.IsNullOrWhiteSpace(module.PartNumber))
    {
        module.HardwareIdentifier =
            $"{module.Manufacturer}|{module.PartNumber}";
    }
    else
    {
        module.HardwareIdentifier =
            $"{module.Manufacturer}|{module.MemoryType}|{module.CapacityGB:F2}GB|{module.Slot}";
    }

    modules.Add(module);

    return modules;
}

        private static string GetSysctlValue(
            string output,
            string key)
        {
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (!trimmed.StartsWith(key + ":"))
                    continue;

                return GetValueAfterColon(trimmed);
            }

            return string.Empty;
        }

        private static string GetValueAfterColon(
            string value)
        {
            var index = value.IndexOf(':');

            if (index < 0)
                return string.Empty;

            return value[(index + 1)..].Trim();
        }

        private static double ParseMemorySize(
            string value)
        {
            var cleaned = value
                .Replace("GB", "", StringComparison.OrdinalIgnoreCase)
                .Replace("MB", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (!double.TryParse(
                cleaned,
                out var number))
            {
                return 0;
            }

            if (value.Contains(
                "MB",
                StringComparison.OrdinalIgnoreCase))
            {
                return number / 1024;
            }

            return number;
        }

        private static long ParseSpeed(string value)
        {
            var cleaned = value
                .Replace("MHz", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            return long.TryParse(
                cleaned,
                out var speed)
                ? speed
                : 0;
        }

        private async Task<string> Run(
            string command,
            params string[] arguments)
        {
            var result = await _commandRunner.RunAsync(
                command,
                arguments);

            if (!result.Success)
                return string.Empty;

            return result.StandardOutput;
        }

        private static string GetCommandValue(string output)
        {
            return output.Trim();
        }

        private static string GetIoregValue(
            string output,
            string key)
        {
            foreach (var line in output.Split('\n'))
            {
                if (!line.Contains(key))
                    continue;

                var separator = line.IndexOf('=');

                if (separator < 0)
                    continue;

                return line[(separator + 1)..]
                    .Trim()
                    .Trim('"');
            }

            return string.Empty;
        }

        private static string GetSystemProfilerValue(
            string output,
            string key)
        {
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();

                if (!trimmed.StartsWith(key + ":"))
                    continue;

                return trimmed[(key.Length + 1)..]
                    .Trim();
            }

            return string.Empty;
        }
    }
