using AssetManagerAgent.Core.History;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Services.History;

public sealed class InventorySnapshotBuilder
{
    public InventorySnapshot Build(Asset asset)
    {
        var snapshot = new InventorySnapshot
        {
            AssetId = asset.AssetId,
	FirstDetected = asset.FirstDetected,
            ScanTime = asset.LastScan
        };

        foreach (var component in GetComponents(asset))
        {
            snapshot.Components.Add(
                new ComponentSnapshot
                {
                    ComponentId = component.ComponentId,
                    AssetId = component.AssetId,
                    ComponentType = component.ComponentType,
                    Manufacturer = component.Manufacturer,
                    Model = component.Model,
                    SerialNumber = component.SerialNumber,
		PartNumber = component.PartNumber,
		Slot = component is MemoryModule memory
    ? memory.Slot
    : string.Empty,
                    HardwareIdentifier =
                        component.HardwareIdentifier,
                    FirstDetected = component.FirstDetected,
                    LastDetected = component.LastDetected
                });
        }

        return snapshot;
    }

    private static IEnumerable<ComponentInformation> GetComponents(
        Asset asset)
    {
        foreach (var item in asset.Memory)
            yield return item;

        foreach (var item in asset.Storage)
            yield return item;

        foreach (var item in asset.Batteries)
            yield return item;

        if (asset.Motherboard != null)
        yield return asset.Motherboard;

        foreach (var item in asset.Monitors)
            yield return item;

        foreach (var item in asset.NetworkAdapters)
            yield return item;

        foreach (var item in asset.Gpus)
            yield return item;

        foreach (var item in asset.Peripherals)
            yield return item;
    }
}
