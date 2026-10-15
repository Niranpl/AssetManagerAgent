namespace AssetManagerAgent.Core.Models;

public class CpuInformation
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public int PhysicalCores { get; set; }
    public int LogicalProcessors { get; set; }
    public double MaxGHz { get; set; }
}

public class MemoryModule : ComponentInformation
{
    public double CapacityGB { get; set; }
    public string MemoryType { get; set; } = string.Empty;
    public long SpeedMHz { get; set; }
    public string Slot { get; set; } = string.Empty;
public int TotalSlots { get; set; }
public int UsedSlots { get; set; }
public int EmptySlots { get; set; }
}

public class StorageDevice : ComponentInformation
{
    public string Name { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public double CapacityGB { get; set; }
    public double UsedGB { get; set; }
    public double FreeGB { get; set; }
    public string FileSystem { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string SmartStatus { get; set; } = string.Empty;
    public string PhysicalIdentifier { get; set; } = string.Empty;
    public bool Internal { get; set; }
    public bool SolidState { get; set; }
    public bool Virtual { get; set; }
    public string FirmwareRevision { get; set; } = string.Empty;
    public bool TrimSupported { get; set; }
    public bool HardwareAesSupported { get; set; }
    public List<StorageVolume> Volumes { get; set; } = [];
}

public class StorageVolume
{
    public string Name { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public string BsdName { get; set; } = string.Empty;
    public string VolumeUuid { get; set; } = string.Empty;
    public double CapacityGB { get; set; }
    public double UsedGB { get; set; }
    public double FreeGB { get; set; }
    public bool Writable { get; set; }
}

public class BatteryInformation : ComponentInformation
{
public int CycleCount { get; set; }
    public int CurrentChargePercentage { get; set; }
    public int HealthPercentage { get; set; }

    public long DesignCapacityMWh { get; set; }
    public long FullChargeCapacityMWh { get; set; }
    public double HealthPercentageCalculated { get; set; }
    public bool ReplacementRecommended { get; set; }
    public DateTime LastHealthChecked { get; set; }

    public string Condition { get; set; } = string.Empty;
    public bool Charging { get; set; }
    public string PowerSource { get; set; } = string.Empty;
    public string RemainingTime { get; set; } = string.Empty;}

public class MotherboardInformation : ComponentInformation
{
    public string BiosManufacturer { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
}

public class BiosInformation
{
    public string Manufacturer { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
}

public class MonitorInformation : ComponentInformation
{
    public string Name { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public double SizeInches { get; set; }
    public bool Internal { get; set; }
    public bool MainDisplay { get; set; }
    public bool Online { get; set; }
    public string DisplayType { get; set; } = string.Empty;
}

public class NetworkAdapter : ComponentInformation
{
    public string Interface { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string IPv4Address { get; set; } = string.Empty;
    public List<string> IPv6Addresses { get; set; } = [];
    public bool Connected { get; set; }
}

public class GpuInformation : ComponentInformation
{
public string DriverVersion { get; set; } = string.Empty;	
    public string Name { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string Bus { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int CoreCount { get; set; }
    public string MetalSupport { get; set; } = string.Empty;
    public double MemoryGB { get; set; }
}

public class PeripheralInformation : ComponentInformation
{
public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string PnpDeviceId { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public bool Connected { get; set; }
}
public class SoftwareInformation
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string LastModified { get; set; } = string.Empty;
    public List<string> SignedBy { get; set; } = [];
}
