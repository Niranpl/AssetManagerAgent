namespace AssetManagerAgent.Core.History;

public sealed class InventoryChangeDetector
{
    public List<ComponentChange> DetectChanges(
        InventorySnapshot? previous,
        InventorySnapshot current)
    {
        var changes = new List<ComponentChange>();

        if (previous == null)
        {
            foreach (var component in current.Components)
            {
                if (string.IsNullOrWhiteSpace(component.ComponentId))
                    continue;

                changes.Add(new ComponentChange
                {
                    ChangeType = ComponentChangeType.Added,

                    AssetId = component.AssetId,
                    ComponentType = component.ComponentType,
                    ComponentId = component.ComponentId,

                    NewComponentId = component.ComponentId,

                    Manufacturer = component.Manufacturer,
                    Model = component.Model,
                    SerialNumber = component.SerialNumber,
                    PartNumber = component.PartNumber,
                    Slot = component.Slot,
                    HardwareIdentifier = component.HardwareIdentifier,

                    Description =
                        $"New {component.ComponentType} detected: " +
                        $"{component.ComponentId}",

                    DetectedAt = current.ScanTime
                });
            }

            return changes;
        }

        var previousById =
            previous.Components
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.ComponentId))
                .GroupBy(
                    x => x.ComponentId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.OrdinalIgnoreCase);

        var currentById =
            current.Components
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.ComponentId))
                .GroupBy(
                    x => x.ComponentId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.OrdinalIgnoreCase);

        var replacedPreviousIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        var replacedCurrentIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        // REPLACED COMPONENTS
        //
        // A replacement is detected when the same asset has
        // the same component type in the same physical slot,
        // but the component ID has changed.
        foreach (var previousComponent in previousById.Values)
        {
            if (string.IsNullOrWhiteSpace(previousComponent.Slot) ||
    !string.Equals(
        previousComponent.ComponentType,
        "RAM",
        StringComparison.OrdinalIgnoreCase))
{
    continue;
}

            var replacement =
                currentById.Values.FirstOrDefault(currentComponent =>
			!previousById.ContainsKey(currentComponent.ComponentId) &&
                    
                    !replacedCurrentIds.Contains(currentComponent.ComponentId) &&
                    string.Equals(
                        currentComponent.AssetId,
                        previousComponent.AssetId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        currentComponent.ComponentType,
                        previousComponent.ComponentType,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        currentComponent.Slot,
                        previousComponent.Slot,
                        StringComparison.OrdinalIgnoreCase));

            if (replacement == null)
                continue;

            replacedPreviousIds.Add(
                previousComponent.ComponentId);

            replacedCurrentIds.Add(
                replacement.ComponentId);

            changes.Add(new ComponentChange
            {
                ChangeType = ComponentChangeType.Replaced,

                AssetId = replacement.AssetId,
                ComponentType = replacement.ComponentType,

                ComponentId = replacement.ComponentId,

                OldComponentId =
                    previousComponent.ComponentId,

                NewComponentId =
                    replacement.ComponentId,

                PreviousAssetId =
                    previousComponent.AssetId,

                PreviousComponentId =
                    previousComponent.ComponentId,

                Manufacturer =
                    replacement.Manufacturer,

                Model =
                    replacement.Model,

                SerialNumber =
                    replacement.SerialNumber,

                PartNumber =
                    replacement.PartNumber,

                Slot =
                    replacement.Slot,

                HardwareIdentifier =
                    replacement.HardwareIdentifier,

                Description =
                    $"{replacement.ComponentType} replaced in slot " +
                    $"{replacement.Slot}: " +
                    $"{previousComponent.ComponentId} → " +
                    $"{replacement.ComponentId}",

                DetectedAt = current.ScanTime
            });
        }

        // NEW COMPONENTS
        foreach (var component in currentById.Values)
        {
            if (replacedCurrentIds.Contains(component.ComponentId))
                continue;

            if (!previousById.ContainsKey(component.ComponentId))
            {
                changes.Add(new ComponentChange
                {
                    ChangeType = ComponentChangeType.Added,

                    AssetId = component.AssetId,
                    ComponentType = component.ComponentType,
                    ComponentId = component.ComponentId,

                    NewComponentId = component.ComponentId,

                    Manufacturer = component.Manufacturer,
                    Model = component.Model,
                    SerialNumber = component.SerialNumber,
                    PartNumber = component.PartNumber,
                    Slot = component.Slot,
                    HardwareIdentifier = component.HardwareIdentifier,

                    Description =
                        $"New {component.ComponentType} detected: " +
                        $"{component.ComponentId}",

                    DetectedAt = current.ScanTime
                });
            }
        }

        // REMOVED COMPONENTS
        foreach (var component in previousById.Values)
        {
            if (replacedPreviousIds.Contains(component.ComponentId))
                continue;

            if (!currentById.ContainsKey(component.ComponentId))
            {
                changes.Add(new ComponentChange
                {
                    ChangeType = ComponentChangeType.Removed,

                    AssetId = component.AssetId,
                    ComponentType = component.ComponentType,
                    ComponentId = component.ComponentId,

                    OldComponentId =
                        component.ComponentId,

                    Manufacturer = component.Manufacturer,
                    Model = component.Model,
                    SerialNumber = component.SerialNumber,
                    PartNumber = component.PartNumber,
                    Slot = component.Slot,
                    HardwareIdentifier =
                        component.HardwareIdentifier,

                    PreviousAssetId =
                        component.AssetId,

                    PreviousComponentId =
                        component.ComponentId,

                    Description =
                        $"{component.ComponentType} no longer detected: " +
                        $"{component.ComponentId}",

                    DetectedAt = current.ScanTime
                });
            }
        }

        // MOVED COMPONENTS
        foreach (var component in currentById.Values)
        {
            if (!previousById.TryGetValue(
                    component.ComponentId,
                    out var previousComponent))
            {
                continue;
            }

            if (!string.Equals(
                    previousComponent.AssetId,
                    component.AssetId,
                    StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new ComponentChange
                {
                    ChangeType = ComponentChangeType.Moved,

                    AssetId = component.AssetId,
                    ComponentType = component.ComponentType,
                    ComponentId = component.ComponentId,

                    OldComponentId =
                        previousComponent.ComponentId,

                    NewComponentId =
                        component.ComponentId,

                    PreviousAssetId =
                        previousComponent.AssetId,

                    PreviousComponentId =
                        previousComponent.ComponentId,

                    Manufacturer =
                        component.Manufacturer,

                    Model =
                        component.Model,

                    SerialNumber =
                        component.SerialNumber,

                    PartNumber =
                        component.PartNumber,

                    Slot =
                        component.Slot,

                    HardwareIdentifier =
                        component.HardwareIdentifier,

                    Description =
                        $"{component.ComponentType} moved from " +
                        $"{previousComponent.AssetId} to " +
                        $"{component.AssetId}",

                    DetectedAt = current.ScanTime
                });
            }
        }

        return changes;
    }
}