using AssetManagerAgent.Core.History;
using AssetManagerAgent.Core.Models;
using AssetManagerAgent.Core.Services;

namespace AssetManagerAgent.Services;

public sealed class ComponentRegistrationService
{
private readonly InventorySnapshot? _previousSnapshot;   
 private readonly AssetIdentityService _assetIdentityService;
    private readonly ComponentIdentityResolver _identityResolver;
    private readonly Dictionary<string, DateTime> _componentRegistry;

    public ComponentRegistrationService(
        InventorySnapshot? previousSnapshot = null,
        Dictionary<string, DateTime>? componentRegistry = null)
    {
	_previousSnapshot = previousSnapshot;
        _assetIdentityService = new AssetIdentityService();
        _identityResolver = new ComponentIdentityResolver();

        _componentRegistry =
            componentRegistry ??
            new Dictionary<string, DateTime>(
                StringComparer.OrdinalIgnoreCase);
    }

    public void RegisterAssetComponents(Asset asset)
{
    RegisterMemory(asset);
    RegisterStorage(asset);
    RegisterBatteries(asset);
    RegisterMotherboard(asset);
    RegisterGpus(asset);
    RegisterMonitors(asset);
    RegisterNetworkAdapters(asset);
RegisterPeripherals(asset);
}

    private void RegisterMemory(Asset asset)
    {
        foreach (var memory in asset.Memory)
        {
            RegisterComponent(
                asset,
                memory,
                "RAM",
                memory.SerialNumber,
                memory.Manufacturer,
                memory.Model,
                memory.PartNumber,
                BuildMemoryIdentifier(memory));
        }
    }

    private void RegisterStorage(Asset asset)
    {
        foreach (var storage in asset.Storage)
        {
            RegisterComponent(
                asset,
                storage,
                "DISK",
                storage.SerialNumber,
                storage.Manufacturer,
                storage.Model,
                storage.PartNumber,
                storage.PhysicalIdentifier);
        }
    }

    private void RegisterBatteries(Asset asset)
    {
        foreach (var battery in asset.Batteries)
        {
            RegisterComponent(
                asset,
                battery,
                "BAT",
                battery.SerialNumber,
                battery.Manufacturer,
                battery.Model,
                battery.PartNumber,
                battery.Model);
        }
    }
        private void RegisterMotherboard(Asset asset)
{
    var motherboard = asset.Motherboard;

    if (motherboard == null)
        return;

    var hasIdentity =
        !string.IsNullOrWhiteSpace(motherboard.SerialNumber) ||
        !string.IsNullOrWhiteSpace(motherboard.HardwareIdentifier) ||
        !string.IsNullOrWhiteSpace(motherboard.Model) ||
        !string.IsNullOrWhiteSpace(motherboard.Manufacturer);

    if (!hasIdentity)
        return;

    RegisterComponent(
        asset,
        motherboard,
        "BOARD",
        motherboard.SerialNumber,
        motherboard.Manufacturer,
        motherboard.Model,
        motherboard.PartNumber,
        motherboard.HardwareIdentifier);
}

    private void RegisterGpus(Asset asset)
    {
        foreach (var gpu in asset.Gpus)
        {
            RegisterComponent(
                asset,
                gpu,
                "GPU",
                gpu.SerialNumber,
                gpu.Manufacturer,
                gpu.Model,
                gpu.PartNumber,
                BuildGpuIdentifier(gpu));
        }
    }

    private void RegisterMonitors(Asset asset)
    {
        foreach (var monitor in asset.Monitors)
        {
            RegisterComponent(
                asset,
                monitor,
                "MON",
                monitor.SerialNumber,
                monitor.Manufacturer,
                monitor.Model,
                monitor.PartNumber,
                BuildMonitorIdentifier(monitor));
        }
    }

    private void RegisterNetworkAdapters(Asset asset)
    {
        foreach (var adapter in asset.NetworkAdapters)
        {
            var hardwareIdentifier =
                !string.IsNullOrWhiteSpace(adapter.MacAddress)
                    ? adapter.MacAddress
                    : $"{adapter.Interface}|{adapter.Name}|{adapter.Type}";

            RegisterComponent(
                asset,
                adapter,
                "NIC",
                adapter.SerialNumber,
                adapter.Manufacturer,
                adapter.Model,
                null,
                hardwareIdentifier);

            adapter.IdentitySource =
                !string.IsNullOrWhiteSpace(adapter.SerialNumber)
                    ? "SerialNumber"
                    : !string.IsNullOrWhiteSpace(adapter.MacAddress)
                        ? "MACAddress"
                        : "InterfaceIdentity";
        }
    }

	private void RegisterPeripherals(Asset asset)
{
    foreach (var peripheral in asset.Peripherals)
    {
        if (string.IsNullOrWhiteSpace(peripheral.HardwareIdentifier))
            continue;

        RegisterComponent(
            asset,
            peripheral,
            "PER",
            peripheral.SerialNumber,
            peripheral.Manufacturer,
            peripheral.Model,
            peripheral.PartNumber,
            peripheral.HardwareIdentifier);

        peripheral.IdentitySource =
            !string.IsNullOrWhiteSpace(peripheral.SerialNumber)
                ? "SerialNumber"
                : "HardwareIdentifier";
    }
}

    private void RegisterComponent(
        Asset asset,
        ComponentInformation component,
        string componentType,
        string? serialNumber,
        string? manufacturer,
        string? model,
        string? partNumber,
        string? hardwareIdentifier)
    {
        var identity = _identityResolver.Resolve(
            componentType,
            serialNumber,
            manufacturer,
            model,
            partNumber,
            hardwareIdentifier);

        component.ComponentId =
            _assetIdentityService.GenerateComponentId(
                componentType,
                identity);

        component.AssetId = asset.AssetId;
        component.ComponentType = componentType;

        component.Manufacturer =
            manufacturer ?? string.Empty;

        component.Model =
            model ?? string.Empty;

        component.SerialNumber =
            serialNumber ?? string.Empty;

        component.PartNumber =
            partNumber ?? string.Empty;

        component.HardwareIdentifier =
            hardwareIdentifier ?? string.Empty;

        component.Status = "Active";
	
	
	
var now = DateTime.UtcNow;

if (_componentRegistry.TryGetValue(
        component.ComponentId,
        out var registeredFirstDetected) &&
    registeredFirstDetected != DateTime.MinValue)
{
    component.FirstDetected = registeredFirstDetected;
}
else
{
    var previousComponent =
        _previousSnapshot?.Components
            .FirstOrDefault(x =>
                string.Equals(
                    x.ComponentId,
                    component.ComponentId,
                    StringComparison.OrdinalIgnoreCase));

    component.FirstDetected =
        previousComponent?.FirstDetected ?? now;

    _componentRegistry[component.ComponentId] =
        component.FirstDetected;
}

component.LastDetected = now;

        if (string.IsNullOrWhiteSpace(component.IdentitySource))
        {
            component.IdentitySource =
                !string.IsNullOrWhiteSpace(serialNumber)
                    ? "SerialNumber"
                    : !string.IsNullOrWhiteSpace(hardwareIdentifier)
                        ? "HardwareIdentifier"
                        : !string.IsNullOrWhiteSpace(partNumber)
                            ? "PartNumber"
                            : "ManufacturerModel";
        }
    }

private static string BuildMemoryIdentifier(
    MemoryModule memory)
{
    var parts = new List<string>();

    if (!string.IsNullOrWhiteSpace(memory.Manufacturer))
        parts.Add(memory.Manufacturer.Trim());

    if (!string.IsNullOrWhiteSpace(memory.PartNumber))
        parts.Add(memory.PartNumber.Trim());

    if (!string.IsNullOrWhiteSpace(memory.Model))
        parts.Add(memory.Model.Trim());

    if (!string.IsNullOrWhiteSpace(memory.MemoryType))
        parts.Add(memory.MemoryType.Trim());

    if (!string.IsNullOrWhiteSpace(memory.Slot))
        parts.Add(memory.Slot.Trim());

    if (memory.CapacityGB > 0)
    {
        parts.Add(
            memory.CapacityGB
                .ToString(
                    "0.##",
                    System.Globalization.CultureInfo.InvariantCulture)
            + "GB");
    }

    if (parts.Count > 0)
    {
        return string.Join("|", parts);
    }

    throw new InvalidOperationException(
        "Unable to create a stable identity for the memory module.");
}
    private static string BuildGpuIdentifier(
        GpuInformation gpu)
    {
        return string.Join(
            "|",
            gpu.Manufacturer,
            gpu.Model,
            gpu.Name);
    }

    private static string BuildMonitorIdentifier(
        MonitorInformation monitor)
    {
        return string.Join(
            "|",
            monitor.Manufacturer,
            monitor.Model,
            monitor.Name,
            monitor.Resolution,
            monitor.ConnectionType,
            monitor.Internal);
    }
}

