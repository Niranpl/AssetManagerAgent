namespace AssetManagerAgent.Core.Services;

public class ComponentIdentityResolver
{
    public string Resolve(
        string componentType,
        string? serialNumber = null,
        string? manufacturer = null,
        string? model = null,
        string? partNumber = null,
        string? hardwareIdentifier = null)
    {
        var type =
            componentType.Trim().ToUpperInvariant();

        return type switch
        {
            "RAM" =>
                ResolveRam(
                    serialNumber,
                    partNumber,
                    manufacturer,
                    model,
                    hardwareIdentifier),

            "DISK" =>
                ResolveStorage(
                    serialNumber,
                    hardwareIdentifier,
                    partNumber,
                    manufacturer,
                    model),

            "BAT" =>
                ResolveBattery(
                    serialNumber,
                    hardwareIdentifier,
                    manufacturer,
                    model),

            "MON" =>
                ResolveMonitor(
                    serialNumber,
                    hardwareIdentifier,
                    partNumber,
                    manufacturer,
                    model),

            "NIC" =>
                ResolveNetwork(
                    serialNumber,
                    hardwareIdentifier,
                    manufacturer,
                    model),

            "GPU" =>
                ResolveGeneric(
                    serialNumber,
                    hardwareIdentifier,
                    partNumber,
                    manufacturer,
                    model),

            _ =>
                ResolveGeneric(
                    serialNumber,
                    hardwareIdentifier,
                    partNumber,
                    manufacturer,
                    model)
        };
    }

    private static string ResolveRam(
        string? serialNumber,
        string? partNumber,
        string? manufacturer,
        string? model,
        string? hardwareIdentifier)
    {
        // Preferred:
        // 1. Physical serial
        // 2. Part number
        // 3. Stable hardware identifier
        // 4. Manufacturer + model

        return FirstValid(
            serialNumber,
            partNumber,
            hardwareIdentifier,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string ResolveStorage(
        string? serialNumber,
        string? hardwareIdentifier,
        string? partNumber,
        string? manufacturer,
        string? model)
    {
        return FirstValid(
            serialNumber,
            hardwareIdentifier,
            partNumber,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string ResolveBattery(
        string? serialNumber,
        string? hardwareIdentifier,
        string? manufacturer,
        string? model)
    {
        return FirstValid(
            serialNumber,
            hardwareIdentifier,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string ResolveMonitor(
        string? serialNumber,
        string? hardwareIdentifier,
        string? partNumber,
        string? manufacturer,
        string? model)
    {
        return FirstValid(
            serialNumber,
            hardwareIdentifier,
            partNumber,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string ResolveNetwork(
        string? serialNumber,
        string? hardwareIdentifier,
        string? manufacturer,
        string? model)
    {
        return FirstValid(
            serialNumber,
            hardwareIdentifier,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string ResolveGeneric(
        string? serialNumber,
        string? hardwareIdentifier,
        string? partNumber,
        string? manufacturer,
        string? model)
    {
        return FirstValid(
            serialNumber,
            hardwareIdentifier,
            partNumber,
            BuildManufacturerModelIdentity(
                manufacturer,
                model));
    }

    private static string FirstValid(
        params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        throw new ArgumentException(
            "Unable to determine a stable component identity.");
    }

    private static string? BuildManufacturerModelIdentity(
        string? manufacturer,
        string? model)
    {
        if (string.IsNullOrWhiteSpace(manufacturer) &&
            string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        return $"{manufacturer?.Trim()}|{model?.Trim()}";
    }
}
