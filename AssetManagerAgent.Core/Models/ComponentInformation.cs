namespace AssetManagerAgent.Core.Models;

public class ComponentInformation
{
    public string ComponentId { get; set; } = string.Empty;

    public string AssetId { get; set; } = string.Empty;

    public string ComponentType { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string PartNumber { get; set; } = string.Empty;

    public string HardwareIdentifier { get; set; } = string.Empty;

    public string IdentitySource { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public DateTime FirstDetected { get; set; }

    public DateTime LastDetected { get; set; }
}
