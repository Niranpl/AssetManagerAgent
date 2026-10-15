using System.Text.Json;
using AssetManagerAgent.Collectors.MacOS;
using AssetManagerAgent.Collectors.Windows;
using AssetManagerAgent.Core.History;
using AssetManagerAgent.Services;
using AssetManagerAgent.Services.Configuration;
using AssetManagerAgent.Services.History;
using AssetManagerAgent.Services.Platform;
using AssetManagerAgent.Services.Health;

var silentMode =
    args.Any(x =>
        string.Equals(
            x,
            "--silent",
            StringComparison.OrdinalIgnoreCase));

if (silentMode)
{
    Console.SetOut(System.IO.TextWriter.Null);
}

AssetManagerAgent.Core.Models.Asset asset;

if (OperatingSystem.IsWindows())
{
    Console.WriteLine("========================================");
    Console.WriteLine("       ASSET MANAGER AGENT");
    Console.WriteLine("       Windows Inventory Scan");
    Console.WriteLine("========================================");
    Console.WriteLine();

    Console.WriteLine("Collecting asset information...");

    var collector = new WindowsCollector();
    asset = collector.Collect();
}
else if (OperatingSystem.IsMacOS())
{
    var commandRunner = new CommandRunner();
    var collector = new MacOSCollector(commandRunner);

    Console.WriteLine("========================================");
    Console.WriteLine("       ASSET MANAGER AGENT");
    Console.WriteLine("       macOS Inventory Scan");
    Console.WriteLine("========================================");
    Console.WriteLine();

    Console.WriteLine("Collecting asset information...");

    asset = await collector.CollectAsync();
}
else
{
    Console.WriteLine("This platform is not supported yet.");
    return;
}

Console.WriteLine();
Console.WriteLine("----------------------------------------");
Console.WriteLine("RAW NETWORK ADAPTERS");
Console.WriteLine("----------------------------------------");

foreach (var adapter in asset.NetworkAdapters)
{
    Console.WriteLine(
        $"Interface={adapter.Interface} | " +
        $"Name={adapter.Name} | " +
        $"Type={adapter.Type} | " +
        $"MAC={adapter.MacAddress} | " +
        $"IPv4={adapter.IPv4Address} | " +
        $"Connected={adapter.Connected}");
}

Console.WriteLine();

var assetIdentityService =
    new AssetManagerAgent.Core.Services.AssetIdentityService();

var assetIdentity =
    !string.IsNullOrWhiteSpace(asset.SerialNumber)
        ? asset.SerialNumber
        : asset.ModelIdentifier;

if (string.IsNullOrWhiteSpace(assetIdentity))
    throw new InvalidOperationException(
        "Unable to determine a stable asset identity.");

asset.AssetId =
    assetIdentityService.GenerateAssetId(
        assetIdentity);

var historyStore =
    new InventoryHistoryStore();

var previousSnapshot =
    await historyStore.LoadAsync();

var now = DateTime.UtcNow;

if (previousSnapshot?.FirstDetected is DateTime previousFirstDetected &&
    previousFirstDetected != DateTime.MinValue)
{
    asset.FirstDetected = previousFirstDetected;
}
else if (previousSnapshot?.Components.Count > 0)
{
    asset.FirstDetected =
        previousSnapshot.Components
            .Where(c => c.FirstDetected != DateTime.MinValue)
            .Select(c => c.FirstDetected)
            .DefaultIfEmpty(now)
            .Min();
}
else
{
    asset.FirstDetected = now;
}

asset.LastScan = now;

var componentRegistryStore =
    new ComponentRegistryStore();

var permanentComponentRegistry =
    await componentRegistryStore.LoadAsync();

var registrationService =
    new ComponentRegistrationService(
        previousSnapshot,
        permanentComponentRegistry);

registrationService.RegisterAssetComponents(asset);

var healthService = new AssetHealthService();

var assetHealth =
    healthService.Evaluate(asset);

asset.HealthStatus = assetHealth.OverallStatus;
asset.HealthScore = assetHealth.HealthScore;
asset.StorageStatus = assetHealth.StorageStatus;
asset.BatteryStatus = assetHealth.BatteryStatus;
asset.SecurityStatus = assetHealth.SecurityStatus;
asset.TpmStatus = assetHealth.TpmStatus;
asset.SecureBootStatus = assetHealth.SecureBootStatus;
asset.HealthIssues = string.Join(
    Environment.NewLine,
    assetHealth.Issues);
asset.HealthWarnings = string.Join(
    Environment.NewLine,
    assetHealth.Warnings);
asset.HealthCheckedAt = assetHealth.CheckedAt;

Console.WriteLine();
Console.WriteLine("----------------------------------------");
Console.WriteLine("ASSET HEALTH");
Console.WriteLine("----------------------------------------");

Console.WriteLine(
    $"Overall Status : {assetHealth.OverallStatus}");

Console.WriteLine(
    $"Health Score   : {assetHealth.HealthScore}");

Console.WriteLine(
    $"Storage        : {assetHealth.StorageStatus}");

Console.WriteLine(
    $"Battery        : {assetHealth.BatteryStatus}");

Console.WriteLine(
    $"Security       : {assetHealth.SecurityStatus}");

Console.WriteLine(
    $"TPM            : {assetHealth.TpmStatus}");

Console.WriteLine(
    $"Secure Boot    : {assetHealth.SecureBootStatus}");

foreach (var warning in assetHealth.Warnings)
{
    Console.WriteLine(
        $"WARNING        : {warning}");
}

foreach (var issue in assetHealth.Issues)
{
    Console.WriteLine(
        $"ISSUE          : {issue}");
}

Console.WriteLine(
    $"Component registry entries before save: {permanentComponentRegistry.Count}");

await componentRegistryStore.SaveAsync(
    permanentComponentRegistry);

var snapshotBuilder =
    new InventorySnapshotBuilder();

var currentSnapshot =
    snapshotBuilder.Build(asset);
var changeDetector =
    new InventoryChangeDetector();

var changes =
    changeDetector.DetectChanges(
        previousSnapshot,
        currentSnapshot);

Console.WriteLine("----------------------------------------");
Console.WriteLine("ASSET");
Console.WriteLine("----------------------------------------");

Console.WriteLine(
    $"Asset ID       : {asset.AssetId}");

Console.WriteLine(
    $"Computer Name  : {asset.ComputerName}");

Console.WriteLine(
    $"Serial Number  : {asset.SerialNumber}");

Console.WriteLine(
    $"Manufacturer   : {asset.Manufacturer}");

Console.WriteLine(
    $"Model          : {asset.Model}");

Console.WriteLine();

Console.WriteLine("----------------------------------------");
Console.WriteLine("COMPONENTS");
Console.WriteLine("----------------------------------------");

Console.WriteLine(
    $"Total Components: {currentSnapshot.Components.Count}");

foreach (var component in currentSnapshot.Components)
{
    Console.WriteLine(
        $"{component.ComponentType,-16} " +
        $"{component.ComponentId}");
}

Console.WriteLine();

Console.WriteLine("----------------------------------------");
Console.WriteLine("CHANGES");
Console.WriteLine("----------------------------------------");

if (previousSnapshot == null)
{
    Console.WriteLine(
        "Initial inventory baseline created.");

    Console.WriteLine(
        $"Components recorded: {currentSnapshot.Components.Count}");
}
else if (changes.Count == 0)
{
    Console.WriteLine(
        "No component changes detected.");
}
else
{
    foreach (var change in changes)
    {
        Console.WriteLine(
            $"{change.ChangeType,-10} | " +
            $"{change.ComponentType,-16} | " +
            $"{change.Description}");
    }
}

var snapshotSaved = false;

if (OperatingSystem.IsWindows())
{
    var configuration =
        WindowsAgentConfiguration.Load();

    Console.WriteLine(
        string.IsNullOrWhiteSpace(configuration.InventoryApiUrl)
            ? "API: URL MISSING"
            : "API: URL EXISTS");

    if (!string.IsNullOrWhiteSpace(configuration.InventoryApiUrl))
    {
        var apiClient =
            new InventoryApiClient(
                configuration.InventoryApiUrl);

        Console.WriteLine("API: Starting upload...");

        var apiSuccess =
            await apiClient.SendAssetAsync(
                asset,
                assetHealth,
                changes);

        if (apiSuccess)
        {
            Console.WriteLine(
                "Asset successfully sent to Power Automate.");

            await historyStore.SaveAsync(
                currentSnapshot);

            snapshotSaved = true;

            Console.WriteLine(
                "Local snapshot saved after successful upload.");
        }
        else
        {
            Console.WriteLine(
                "WARNING: Failed to send asset to Power Automate.");

            Console.WriteLine(
                "Local snapshot was NOT replaced, so changes can be retried.");
        }
    }
    else
    {
        Console.WriteLine(
            "WARNING: Inventory API URL is missing.");

        Console.WriteLine(
            "Local snapshot was NOT replaced.");
    }
}
else
{
    await historyStore.SaveAsync(
        currentSnapshot);

    snapshotSaved = true;
}

Console.WriteLine();
Console.WriteLine("----------------------------------------");
Console.WriteLine("HISTORY");
Console.WriteLine("----------------------------------------");

if (snapshotSaved)
{
    Console.WriteLine(
        "Current snapshot saved successfully.");

    Console.WriteLine(
        "History file: data/inventory-snapshot.json");
}
else
{
    Console.WriteLine(
        "Current snapshot was NOT saved because the inventory upload did not succeed.");
}

Console.WriteLine();

var json = JsonSerializer.Serialize(
    asset,
    new JsonSerializerOptions
    {
        WriteIndented = true
    });

Console.WriteLine(json);