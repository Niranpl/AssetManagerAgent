using AssetManagerAgent.Core.History;
using AssetManagerAgent.Core.Models;
using AssetManagerAgent.Services.Health;

namespace AssetManagerAgent.Services;

public class InventoryApiRequest
{
    public Asset Asset { get; set; } = new();

    public AssetHealth Health { get; set; } = new();

    public List<ComponentChange> Changes { get; set; } = [];

    public string VendorID { get; set; } = string.Empty;

    public string VendorName { get; set; } = string.Empty;
}