using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Core.Interfaces;

public interface IPlatformCollector
{
    bool CanRun();

    string PlatformName { get; }

    Task<Asset> CollectAsync();
}
