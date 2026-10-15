using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Core.Services;

public class AssetIdentityService
{
    public string GenerateAssetId(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException(
                "Serial number is required.",
                nameof(serialNumber));

        var normalized = serialNumber
            .Trim()
            .ToUpperInvariant();

        return $"AST-{CreateStableCode(normalized)}";
    }

    public string GenerateComponentId(
        string componentType,
        string identity)
    {
        if (string.IsNullOrWhiteSpace(componentType))
            throw new ArgumentException(
                "Component type is required.",
                nameof(componentType));

        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException(
                "Component identity is required.",
                nameof(identity));

        var prefix = componentType.Trim().ToUpperInvariant();

        var code = CreateStableCode(
            identity.Trim().ToUpperInvariant());

        return $"{prefix}-{code}";
    }

    private static string CreateStableCode(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash)[..10];
    }
}
