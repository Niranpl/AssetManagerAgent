using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Services.Health;

public sealed class AssetHealthService
{
    public AssetHealth Evaluate(Asset asset)
    {
        var health = new AssetHealth
        {
            CheckedAt = DateTime.UtcNow
        };

        EvaluateStorage(asset, health);
        EvaluateBattery(asset, health);
        EvaluateSecurity(asset, health);

        CalculateOverallStatus(health);

        return health;
    }

    private static void EvaluateStorage(
        Asset asset,
        AssetHealth health)
    {
        if (asset.Storage.Count == 0)
        {
            health.StorageStatus = "NotAvailable";
            return;
        }

        var unhealthy =
            asset.Storage.Any(x =>
                !string.IsNullOrWhiteSpace(x.SmartStatus) &&
                !string.Equals(
                    x.SmartStatus,
                    "Healthy",
                    StringComparison.OrdinalIgnoreCase));

        var lowSpace =
            asset.Storage.Any(x =>
                x.CapacityGB > 0 &&
                (x.FreeGB / x.CapacityGB) * 100 < 10);

        if (unhealthy)
        {
            health.StorageStatus = "Critical";
            health.Issues.Add(
                "One or more storage devices report an unhealthy SMART status.");
        }
        else if (lowSpace)
        {
            health.StorageStatus = "Warning";
            health.Warnings.Add(
                "One or more storage devices have less than 10% free capacity.");
        }
        else
        {
            health.StorageStatus = "Healthy";
        }
    }

    private static void EvaluateBattery(
        Asset asset,
        AssetHealth health)
    {
        if (asset.Batteries.Count == 0)
        {
            health.BatteryStatus = "NotAvailable";
            return;
        }

        var replacement =
            asset.Batteries.Any(x =>
                x.ReplacementRecommended);

        var poorHealth =
            asset.Batteries.Any(x =>
                x.HealthPercentageCalculated > 0 &&
                x.HealthPercentageCalculated < 50);

        if (replacement || poorHealth)
        {
            health.BatteryStatus = "Warning";

            health.Warnings.Add(
                "Battery health indicates that service or replacement may be required.");
        }
        else
        {
            health.BatteryStatus = "Healthy";
        }
    }

    private static void EvaluateSecurity(
        Asset asset,
        AssetHealth health)
    {
        health.TpmStatus =
            asset.TpmEnabled
                ? "Enabled"
                : "Disabled";

        health.SecureBootStatus =
            asset.SecureBoot
                ? "Enabled"
                : "Disabled";

        var securityIssues = new List<string>();

        if (!asset.TpmEnabled)
        {
            securityIssues.Add("TPM is disabled or unavailable.");
        }

        if (!asset.SecureBoot)
        {
            securityIssues.Add("Secure Boot is disabled.");
        }

        if (securityIssues.Count == 0)
        {
            health.SecurityStatus = "Healthy";
        }
        else
        {
            health.SecurityStatus = "Warning";

            health.Warnings.AddRange(
                securityIssues);
        }
    }

    private static void CalculateOverallStatus(
        AssetHealth health)
    {
        if (health.Issues.Count > 0)
        {
            health.OverallStatus = "Critical";
        }
        else if (health.Warnings.Count > 0)
        {
            health.OverallStatus = "Warning";
        }
        else
        {
            health.OverallStatus = "Healthy";
        }

        var score = 100;

        if (health.StorageStatus == "Critical")
            score -= 40;
        else if (health.StorageStatus == "Warning")
            score -= 15;

        if (health.BatteryStatus == "Warning")
            score -= 15;

        if (health.SecurityStatus == "Warning")
            score -= 15;

        health.HealthScore =
            Math.Max(0, score);
    }
}