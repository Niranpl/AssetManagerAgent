using System.Text.Json;

namespace AssetManagerAgent.Services.History;

public sealed class ComponentRegistryStore
{
    private readonly string _filePath;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ComponentRegistryStore()
    {
        var dataDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "data");

        Directory.CreateDirectory(dataDirectory);

        _filePath =
            Path.Combine(
                dataDirectory,
                "component-registry.json");
    }

    public async Task<Dictionary<string, DateTime>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, DateTime>(
                StringComparer.OrdinalIgnoreCase);

        await using var stream =
            File.OpenRead(_filePath);

        var registry =
            await JsonSerializer.DeserializeAsync<
                Dictionary<string, DateTime>>(
                    stream,
                    _jsonOptions);

        return registry ??
            new Dictionary<string, DateTime>(
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(
        Dictionary<string, DateTime> registry)
    {
        await using var stream =
            File.Create(_filePath);

        await JsonSerializer.SerializeAsync(
            stream,
            registry,
            _jsonOptions);
    }
}
