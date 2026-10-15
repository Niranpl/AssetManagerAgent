using System.Text.Json;
using AssetManagerAgent.Core.History;

namespace AssetManagerAgent.Services.History;

public sealed class InventoryHistoryStore
{
    private readonly string _filePath;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public InventoryHistoryStore()
    {
        var dataDirectory =
            Path.Combine(
                AppContext.BaseDirectory,
                "data");

        Directory.CreateDirectory(dataDirectory);

        _filePath =
            Path.Combine(
                dataDirectory,
                "inventory-snapshot.json");
    }

    public async Task<InventorySnapshot?> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return null;

        await using var stream =
            File.OpenRead(_filePath);

        return await JsonSerializer.DeserializeAsync<InventorySnapshot>(
            stream,
            _jsonOptions);
    }

    public async Task SaveAsync(
        InventorySnapshot snapshot)
    {
        await using var stream =
            File.Create(_filePath);

        await JsonSerializer.SerializeAsync(
            stream,
            snapshot,
            _jsonOptions);
    }
}
