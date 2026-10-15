using System.Text.Json;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.MacOS.Software;

public sealed class MacOSSoftwareCollector
{
    public List<SoftwareInformation> Collect(string jsonOutput)
    {
        var software = new List<SoftwareInformation>();

        if (string.IsNullOrWhiteSpace(jsonOutput))
            return software;

        try
        {
            using var document =
                JsonDocument.Parse(jsonOutput);

            if (!document.RootElement.TryGetProperty(
                    "SPApplicationsDataType",
                    out var applications))
            {
                return software;
            }

            foreach (var application in applications.EnumerateArray())
            {
                var name = GetString(
                    application,
                    "_name");

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                software.Add(
                    new SoftwareInformation
                    {
                        Name = name,

                        Version = GetString(
                            application,
                            "version"),

                        Publisher = GetString(
                            application,
                            "obtained_from"),

                        InstallLocation = GetString(
                            application,
                            "path"),

                        Architecture = GetString(
                            application,
                            "arch_kind"),

                        LastModified = GetString(
                            application,
                            "lastModified"),

                        SignedBy = GetStringArray(
                            application,
                            "signed_by")
                    });
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return software
            .GroupBy(
                x => $"{x.Name}|{x.Version}|{x.InstallLocation}",
                StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static string GetString(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.ToString();
    }

    private static List<string> GetStringArray(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
            return [];

        return value
            .EnumerateArray()
            .Where(x =>
                x.ValueKind == JsonValueKind.String)
            .Select(x =>
                x.GetString() ?? string.Empty)
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .ToList();
    }
}