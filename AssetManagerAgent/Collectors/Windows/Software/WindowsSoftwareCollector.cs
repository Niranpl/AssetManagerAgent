using Microsoft.Win32;
using AssetManagerAgent.Core.Models;

namespace AssetManagerAgent.Collectors.Windows.Software;

public class WindowsSoftwareCollector
{
    public List<SoftwareInformation> Collect()
    {
        var software = new List<SoftwareInformation>();

        CollectFromRegistry(
            RegistryHive.LocalMachine,
            RegistryView.Registry64,
            software);

        CollectFromRegistry(
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            software);

        return software
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => $"{x.Name}|{x.Version}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectFromRegistry(
        RegistryHive hive,
        RegistryView view,
        List<SoftwareInformation> software)
    {
        try
        {
            using var baseKey =
                RegistryKey.OpenBaseKey(hive, view);

            using var uninstallKey =
                baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (uninstallKey == null)
                return;

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var subKey =
                        uninstallKey.OpenSubKey(subKeyName);

                    if (subKey == null)
                        continue;

                    var displayName =
                        GetString(subKey, "DisplayName");

                    if (string.IsNullOrWhiteSpace(displayName))
                        continue;

                    var systemComponent =
                        GetString(subKey, "SystemComponent");

                    if (string.Equals(
                        systemComponent,
                        "1",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    software.Add(new SoftwareInformation
                    {
                        Name = displayName,
                        Publisher = GetString(
                            subKey,
                            "Publisher"),

                        Version = GetString(
                            subKey,
                            "DisplayVersion"),

                        LastModified = GetString(
                            subKey,
                            "InstallDate"),

                        InstallLocation = GetString(
                            subKey,
                            "InstallLocation"),

                        Architecture =
                            view == RegistryView.Registry64
                                ? "x64"
                                : "x86"
                    });
                }
                catch
                {
                    // Ignore an individual broken registry entry.
                }
            }
        }
        catch
        {
            // Registry access may be unavailable.
        }
    }

    private static string GetString(
        RegistryKey key,
        string name)
    {
        try
        {
            return key.GetValue(name)
                ?.ToString()
                ?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
