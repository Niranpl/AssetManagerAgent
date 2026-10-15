namespace AssetManagerAgent.Services.Health;

public sealed class AssetHealth
{
    public string OverallStatus { get; set; } = string.Empty;

    public string StorageStatus { get; set; } = string.Empty;

    public string BatteryStatus { get; set; } = string.Empty;

    public string SecurityStatus { get; set; } = string.Empty;

    public string TpmStatus { get; set; } = string.Empty;

    public string SecureBootStatus { get; set; } = string.Empty;

    public int HealthScore { get; set; }

    public List<string> Issues { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public DateTime CheckedAt { get; set; }
}