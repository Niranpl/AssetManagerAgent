using System.Text.Json.Serialization;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Core.History;

public class InventorySnapshot
{
    public string AssetId { get; set; } = string.Empty;
    public DateTime FirstDetected { get; set; }
    public DateTime ScanTime { get; set; }

    public List<ComponentSnapshot> Components { get; set; } = [];
}

public class ComponentSnapshot
{
    public string ComponentId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Slot { get; set; } = string.Empty;
    public string HardwareIdentifier { get; set; } = string.Empty;

    public DateTime FirstDetected { get; set; }
    public DateTime LastDetected { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComponentChangeType
{
    Added,
    Removed,
    Replaced,
    Moved
}

public class ComponentChange
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
public ComponentChangeType ChangeType { get; set; }

    public string AssetId { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;

    public string ComponentId { get; set; } = string.Empty;

    public string OldComponentId { get; set; } = string.Empty;
    public string NewComponentId { get; set; } = string.Empty;

    public string PreviousAssetId { get; set; } = string.Empty;
    public string PreviousComponentId { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;

    public string Slot { get; set; } = string.Empty;
    public string HardwareIdentifier { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime DetectedAt { get; set; }
}