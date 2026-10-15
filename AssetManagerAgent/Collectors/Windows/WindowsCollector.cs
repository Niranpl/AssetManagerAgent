using System.Management;
using System.Diagnostics;
using AssetManagerAgent.Core.Models;
using AssetManagerAgent.Collectors.Windows.Peripherals;
using AssetManagerAgent.Collectors.Windows.Software;

namespace AssetManagerAgent.Collectors.Windows;

public class WindowsCollector
{
    public Asset Collect()
    {
        var asset = new Asset();

Console.WriteLine("Collecting system WMI...");
var systemStart = Stopwatch.GetTimestamp();

        var computer = GetFirst("Win32_ComputerSystem");
        var bios = GetFirst("Win32_BIOS");
        var os = GetFirst("Win32_OperatingSystem");
        var processor = GetFirst("Win32_Processor");
        var systemProduct = GetFirst("Win32_ComputerSystemProduct");
        var motherboard = GetFirst("Win32_BaseBoard");


Console.WriteLine(
    $"System WMI completed in {Stopwatch.GetElapsedTime(systemStart).TotalSeconds:F1}s");

        // =========================
        // SYSTEM
        // =========================
        var biosSerial = GetString(bios, "SerialNumber");

        var invalidSerials = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "",
            "Default string",
            "To be filled by O.E.M.",
            "To be filled by OEM",
            "System Serial Number",
            "None",
            "Unknown",
            "N/A"
        };

        asset.SerialNumber =
            !invalidSerials.Contains(biosSerial.Trim())
                ? biosSerial.Trim()
                : string.Empty;

        asset.ComputerName = Environment.MachineName;
        asset.UserName = Environment.UserName;

        asset.Manufacturer = GetString(computer, "Manufacturer");
        asset.Model = GetString(computer, "Model");
        asset.ModelIdentifier = GetString(systemProduct, "UUID");

        // =========================
        // WINDOWS MANAGEMENT IDENTITY
        // =========================
        asset.DeviceId =
            !string.IsNullOrWhiteSpace(asset.ModelIdentifier)
                ? $"IT-{asset.ModelIdentifier}"
                : $"IT-{asset.SerialNumber}";
        asset.Hostname = Environment.MachineName;

        asset.UserDomain = Environment.UserDomainName;
        asset.Domain = GetString(computer, "Domain");

        asset.EmployeeName = GetWindowsEmployeeName(asset.UserName);

        var agentConfiguration =
            AssetManagerAgent.Services.Configuration.WindowsAgentConfiguration.Load();

        asset.AssignedEmployeeId =
            agentConfiguration.AssignedEmployeeID;

        asset.AssignedEmployeeName =
            agentConfiguration.AssignedEmployeeName;

        asset.DeviceType = GetWindowsDeviceType(os);

        asset.OSBuild = GetString(os, "BuildNumber");

        asset.AgentVersion = "2.0.0";

        // =========================
        // OPERATING SYSTEM
        // =========================
        asset.OperatingSystem = GetString(os, "Caption");
        asset.OperatingSystemVersion = GetString(os, "Version");

        // =========================
        // CPU
        // =========================
        asset.Cpu = new CpuInformation
        {
            Name = GetString(processor, "Name"),
            Manufacturer = GetString(processor, "Manufacturer"),
            PhysicalCores = GetInt(processor, "NumberOfCores"),
            LogicalProcessors = GetInt(
                processor,
                "NumberOfLogicalProcessors"),
            MaxGHz = GetInt(
                processor,
                "MaxClockSpeed") / 1000.0
        };

        // =========================
        // RAM
        // =========================
        asset.Memory = GetMemoryModules();

        // =========================
        // BIOS
        // =========================
        asset.Bios = new BiosInformation
        {
            Manufacturer = GetString(bios, "Manufacturer"),
            Version = GetString(bios, "SMBIOSBIOSVersion"),
            ReleaseDate = GetDateString(
                bios,
                "ReleaseDate")
        };

        // =========================
        // MOTHERBOARD
        // =========================

	var motherboardSerial = GetString(motherboard, "SerialNumber");

var invalidMotherboardSerials = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase)
{
    "",
    "Default string",
    "To be filled by O.E.M.",
    "To be filled by OEM",
    "System Serial Number",
    "None",
    "Unknown",
    "N/A"
};

var validMotherboardSerial =
    !invalidMotherboardSerials.Contains(motherboardSerial.Trim())
        ? motherboardSerial.Trim()
        : string.Empty;

asset.Motherboard = new MotherboardInformation
{
    Manufacturer = GetString(motherboard, "Manufacturer"),
    Model = GetString(motherboard, "Product"),
    SerialNumber = validMotherboardSerial,
    PartNumber = GetString(motherboard, "PartNumber"),
    HardwareIdentifier = BuildMotherboardIdentifier(
        motherboard,
        validMotherboardSerial),
    BiosManufacturer = GetString(bios, "Manufacturer"),
    BiosVersion = GetString(bios, "SMBIOSBIOSVersion")
};
        // =========================
        // BATTERY
        // =========================
        asset.Batteries = GetBatteries();

        // =========================
        // SECURITY
        // =========================
        CollectSecurityInformation(asset);

Console.WriteLine("Collecting storage...");
var storageStart = Stopwatch.GetTimestamp();

asset.Storage =
    new WindowsStorageCollector().Collect();

Console.WriteLine(
    $"Storage completed in {Stopwatch.GetElapsedTime(storageStart).TotalSeconds:F1}s");

// GPU / MONITOR / NETWORK
// =========================
var displayNetworkCollector =
    new WindowsDisplayNetworkCollector();

asset.Gpus =
    displayNetworkCollector.CollectGpus();

asset.Monitors =
    displayNetworkCollector.CollectMonitors();

asset.NetworkAdapters =
    displayNetworkCollector.CollectNetworkAdapters();

var primaryNetworkAdapter =
    displayNetworkCollector.GetPrimaryNetworkAdapter(
        asset.NetworkAdapters);

if (primaryNetworkAdapter != null)
{
    asset.MacAddress =
        primaryNetworkAdapter.MacAddress;

    asset.IPv4Address =
        primaryNetworkAdapter.IPv4Address;

    asset.IPv6Address =
        primaryNetworkAdapter.IPv6Addresses
            .FirstOrDefault() ?? string.Empty;
}

Console.WriteLine("Collecting software...");
var softwareStart = Stopwatch.GetTimestamp();

asset.Software =
    new WindowsSoftwareCollector().Collect();

Console.WriteLine(
    $"Software completed in {Stopwatch.GetElapsedTime(softwareStart).TotalSeconds:F1}s");

Console.WriteLine("Collecting peripherals...");
var peripheralsStart = Stopwatch.GetTimestamp();

asset.Peripherals =
    new WindowsPeripheralCollector().Collect();

Console.WriteLine(
    $"Peripherals completed in {Stopwatch.GetElapsedTime(peripheralsStart).TotalSeconds:F1}s");

return asset;
}
    // =========================================================
    // MEMORY
    // =========================================================

private static List<MemoryModule> GetMemoryModules()
{
    var modules = new List<MemoryModule>();

    var totalSlots = 0;

    try
    {
        using var slotSearcher = new ManagementObjectSearcher(
            "SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");

        foreach (ManagementObject slotInfo in slotSearcher.Get())
        {
            totalSlots += Convert.ToInt32(slotInfo["MemoryDevices"] ?? 0);
        }
    }
    catch
    {
    }

    try
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_PhysicalMemory");

        foreach (ManagementObject item in searcher.Get())
        {
            var capacity = GetLong(item, "Capacity");

            modules.Add(new MemoryModule
            {
                Manufacturer = GetString(item, "Manufacturer"),
                SerialNumber = GetString(item, "SerialNumber"),
                PartNumber = GetString(item, "PartNumber"),
                Slot = GetString(item, "DeviceLocator"),

                CapacityGB = Math.Round(
                    capacity / 1024d / 1024d / 1024d,
                    2),

                SpeedMHz = GetLong(item, "Speed"),
                MemoryType = GetString(item, "SMBIOSMemoryType")
            });
        }
    }
    catch
    {
    }

    if (totalSlots <= 0)
    {
        totalSlots = modules.Count;
    }

    var usedSlots = modules.Count;
    var emptySlots = Math.Max(0, totalSlots - usedSlots);

    foreach (var module in modules)
    {
        module.TotalSlots = totalSlots;
        module.UsedSlots = usedSlots;
        module.EmptySlots = emptySlots;
    }

    return modules;
}

    private static string BuildMemoryIdentifier(
        ManagementObject item)
    {
        var serial = GetString(
            item,
            "SerialNumber");

        if (!string.IsNullOrWhiteSpace(serial))
            return $"SERIAL|{serial}";

        var partNumber = GetString(
            item,
            "PartNumber");

        var slot = GetString(
            item,
            "DeviceLocator");

        if (!string.IsNullOrWhiteSpace(partNumber))
            return $"PART|{partNumber}|SLOT|{slot}";

        var manufacturer = GetString(
            item,
            "Manufacturer");

        var capacity = GetLong(
            item,
            "Capacity");

        var speed = GetLong(
            item,
            "Speed");

        return $"RAM|{manufacturer}|{capacity}|{speed}|{slot}";
    }

    // =========================================================
    // MOTHERBOARD
    // =========================================================

private static string BuildMotherboardIdentifier(
    ManagementObject motherboard,
    string validMotherboardSerial)
{
    if (!string.IsNullOrWhiteSpace(validMotherboardSerial))
        return $"SERIAL|{validMotherboardSerial}";
        var product = GetString(
            motherboard,
            "Product");

        var manufacturer = GetString(
            motherboard,
            "Manufacturer");

        var partNumber = GetString(
            motherboard,
            "PartNumber");

        if (!string.IsNullOrWhiteSpace(partNumber))
            return $"PART|{partNumber}|{manufacturer}|{product}";

        return $"BOARD|{manufacturer}|{product}";
    }

    // =========================================================
    // BATTERY
    // =========================================================

    private static string GetDateString(
    ManagementBaseObject? obj,
    string property)
{
    if (obj == null)
        return string.Empty;

    try
    {
        var value = obj[property];

        if (value == null)
            return string.Empty;

        if (value is DateTime date)
        {
            return date.ToUniversalTime()
                .ToString("O");
        }

        var text = value.ToString();

        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        try
        {
            return ManagementDateTimeConverter
                .ToDateTime(text)
                .ToUniversalTime()
                .ToString("O");
        }
        catch
        {
            return text.Trim();
        }
    }
    catch
    {
        return string.Empty;
    }
}

    private static string BuildBatteryIdentifier(
        ManagementObject battery)
    {
        var serial = GetString(
            battery,
            "SerialNumber");

        if (!string.IsNullOrWhiteSpace(serial))
            return $"SERIAL|{serial}";

        var manufacturer = GetString(
            battery,
            "Manufacturer");

        var name = GetString(
            battery,
            "Name");

        return $"BATTERY|{manufacturer}|{name}";
    }

    private static bool IsCharging(int status)
    {
        // Windows BatteryStatus:
        // 2 = Charging
        // 6 = Charging + High
        // 7 = Charging + Low
        // 8 = Charging + Critical
        return status is 2 or 6 or 7 or 8;
    }

    private static string GetPowerSource(int status)
    {
        return status switch
        {
            1 => "Discharging",
            2 => "Charging",
            3 => "FullyCharged",
            4 => "Critical",
            5 => "Unknown",
            6 => "Charging",
            7 => "Charging",
            8 => "Charging",
            _ => string.Empty
        };
    }

    private static string GetBatteryCondition(int status)
    {
        return status switch
        {
            1 => "Discharging",
            2 => "Charging",
            3 => "FullyCharged",
            4 => "Critical",
            5 => "Unknown",
            6 => "Charging",
            7 => "Charging",
            8 => "Charging",
            _ => string.Empty
        };
    }
private static List<BatteryInformation> GetBatteries()
{
    var batteries = new List<BatteryInformation>();

    using var searcher = new ManagementObjectSearcher(
        "SELECT * FROM Win32_Battery");

    foreach (ManagementObject batteryObject in searcher.Get())
    {
        var statusCode = GetInt(
            batteryObject,
            "BatteryStatus");

        batteries.Add(new BatteryInformation
        {
            Model = GetString(
                batteryObject,
                "Name"),

            Manufacturer = GetString(
                batteryObject,
                "Manufacturer"),

            SerialNumber = GetString(
                batteryObject,
                "SerialNumber"),

            CurrentChargePercentage = GetInt(
                batteryObject,
                "EstimatedChargeRemaining"),

            Condition = GetBatteryCondition(
                statusCode),

            Charging = IsCharging(
                statusCode),

            PowerSource = GetPowerSource(
                statusCode),

            HardwareIdentifier =
                BuildBatteryIdentifier(
                    batteryObject)
        });
    }
    return batteries;
}
private static void EnrichBatteryHealth(
    List<BatteryInformation> batteries)
{
    if (batteries.Count == 0)
        return;

    try
    {
        using var capacitySearcher = new ManagementObjectSearcher(
            @"root\wmi",
            "SELECT DesignedCapacity FROM BatteryStaticData");

        foreach (ManagementObject item in capacitySearcher.Get())
        {
            var capacity = GetLong(item, "DesignedCapacity");

            if (capacity > 0)
            {
                batteries[0].DesignCapacityMWh = capacity;
                break;
            }
        }
    }
    catch
    {
    }

    try
    {
        using var fullChargeSearcher = new ManagementObjectSearcher(
            @"root\wmi",
            "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");

        foreach (ManagementObject item in fullChargeSearcher.Get())
        {
            var capacity = GetLong(
                item,
                "FullChargedCapacity");

            if (capacity > 0)
            {
                batteries[0].FullChargeCapacityMWh = capacity;
                break;
            }
        }
    }
    catch
    {
    }

    try
    {
        using var cycleSearcher = new ManagementObjectSearcher(
            @"root\wmi",
            "SELECT CycleCount FROM BatteryCycleCount");

        foreach (ManagementObject item in cycleSearcher.Get())
        {
            var cycles = GetLong(item, "CycleCount");

            if (cycles >= 0)
            {
                batteries[0].CycleCount = (int)cycles;
                break;
            }
        }
    }
    catch
    {
    }

    var design = batteries[0].DesignCapacityMWh;
    var fullCharge = batteries[0].FullChargeCapacityMWh;

    if (design > 0 && fullCharge > 0)
    {
        var health = Math.Round(
            fullCharge * 100d / design,
            2);

        batteries[0].HealthPercentageCalculated = health;

        batteries[0].ReplacementRecommended =
            health < 50;

        batteries[0].Condition =
            health >= 85 ? "Excellent" :
            health >= 70 ? "Good" :
            health >= 50 ? "Degraded - Monitor" :
            "Critical - Replace";
    }

    batteries[0].LastHealthChecked = DateTime.UtcNow;
}

    // =========================================================
    // TPM / SECURE BOOT
    // =========================================================

    private static void CollectSecurityInformation(Asset asset)
    {
        asset.TpmEnabled = false;
        asset.TpmVersion = string.Empty;
        asset.SecureBoot = false;

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    @"root\CIMV2\Security\MicrosoftTpm",
                    "SELECT * FROM Win32_Tpm");

            foreach (ManagementObject tpm in searcher.Get())
            {
                asset.TpmEnabled = true;

                asset.TpmVersion =
                    GetString(tpm, "ManufacturerVersion");

                break;
            }
        }
        catch
        {
            // TPM information may be unavailable.
        }

        try
        {
            using var process = new Process();

            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -NonInteractive -ExecutionPolicy Bypass " +
                    "-Command \"try { if (Confirm-SecureBootUEFI) { exit 0 } else { exit 1 } } catch { exit 2 }\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            process.WaitForExit();

            asset.SecureBoot = process.ExitCode == 0;
        }
        catch
        {
            // Secure Boot information may be unavailable.
        }
    }

    // =========================================================
    // DEVICE TYPE
    // =========================================================

    private static string GetWindowsDeviceType(
        ManagementBaseObject? os)
    {
        // Windows Server
        if (GetInt(os, "ProductType") != 1)
            return "Server";

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT ChassisTypes FROM Win32_SystemEnclosure");

            foreach (ManagementObject enclosure in searcher.Get())
            {
                if (enclosure["ChassisTypes"] is ushort[] chassisTypes)
                {
                    // Preserve the laptop chassis mapping
                    // used by the previous Windows agent.
                    if (chassisTypes.Any(type =>
                        type is 8 or 9 or 10 or 14))
                    {
                        return "Laptop";
                    }
                }
            }
        }
        catch
        {
            // Collection failure must not stop inventory.
        }

        return "Desktop";
    }

    // =========================================================
    // WINDOWS USER
    // =========================================================

    private static string GetWindowsEmployeeName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return string.Empty;

        try
        {
            using var searcher =
                new ManagementObjectSearcher(
                    "SELECT FullName FROM Win32_UserAccount " +
                    "WHERE Name='" +
                    userName.Replace("'", "''") +
                    "'");

            foreach (ManagementObject account in searcher.Get())
            {
                var fullName = GetString(account, "FullName");

                if (!string.IsNullOrWhiteSpace(fullName))
                    return fullName;
            }
        }
        catch
        {
            // Full name may not be available.
        }

        return userName;
    }

    // =========================================================
    // MEMORY TYPE
    // =========================================================

    private static string GetMemoryType(int value)
    {
        return value switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            26 => "DDR4",
            30 => "LPDDR4",
            31 => "LPDDR5",
            34 => "DDR5",
            _ => value > 0
                ? $"Unknown ({value})"
                : string.Empty
        };
    }

    // =========================================================
    // WMI HELPERS
    // =========================================================

    private static ManagementObject? GetFirst(
        string className)
    {
        using var searcher =
            new ManagementObjectSearcher(
                $"SELECT * FROM {className}");

        return searcher
            .Get()
            .Cast<ManagementObject>()
            .FirstOrDefault();
    }

    private static string GetString(
        ManagementBaseObject? obj,
        string property)
    {
        if (obj == null)
            return string.Empty;

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

    private static int GetInt(
        ManagementBaseObject? obj,
        string property)
    {
        if (obj == null)
            return 0;

        try
        {
            return Convert.ToInt32(
                obj[property]);
        }
        catch
        {
            return 0;
        }
    }

    private static long GetLong(
        ManagementBaseObject? obj,
        string property)
    {
        if (obj == null)
            return 0;

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

    
}
