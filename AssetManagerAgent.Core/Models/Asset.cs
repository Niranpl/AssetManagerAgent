namespace AssetManagerAgent.Core.Models;

public class Asset
{
    public string AssetId { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserDomain { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string AssignedEmployeeId { get; set; } = string.Empty;
    public string AssignedEmployeeName { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;

    public string Hostname { get; set; } = string.Empty;
    public string IPv4Address { get; set; } = string.Empty;
    public string IPv6Address { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;

    public string OSBuild { get; set; } = string.Empty;

    public bool TpmEnabled { get; set; }
    public string TpmVersion { get; set; } = string.Empty;
    public bool SecureBoot { get; set; }

    public string DeviceStatus { get; set; } = string.Empty;
    public string SecurityStatus { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ModelIdentifier { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;
    public string OperatingSystemVersion { get; set; } = string.Empty;

    public CpuInformation Cpu { get; set; } = new();

    public List<MemoryModule> Memory { get; set; } = [];

    public List<StorageDevice> Storage { get; set; } = [];

    public List<BatteryInformation> Batteries { get; set; } = [];

    public MotherboardInformation Motherboard { get; set; } = new();

    public BiosInformation Bios { get; set; } = new();

    public List<MonitorInformation> Monitors { get; set; } = [];

    public List<NetworkAdapter> NetworkAdapters { get; set; } = [];

    public List<GpuInformation> Gpus { get; set; } = [];

    public List<PeripheralInformation> Peripherals { get; set; } = [];

    public List<SoftwareInformation> Software { get; set; } = [];

    public DateTime FirstDetected { get; set; }

    public DateTime LastScan { get; set; }

    public string HealthStatus { get; set; } = string.Empty;
    public int HealthScore { get; set; }
    public string StorageStatus { get; set; } = string.Empty;
    public string BatteryStatus { get; set; } = string.Empty;
    public string TpmStatus { get; set; } = string.Empty;
    public string SecureBootStatus { get; set; } = string.Empty;
    public string HealthIssues { get; set; } = string.Empty;
    public string HealthWarnings { get; set; } = string.Empty;
    public DateTime HealthCheckedAt { get; set; }
}
