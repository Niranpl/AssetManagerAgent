using System.Text.Json;

namespace AssetManagerAgent.Services.Configuration;

public class WindowsAgentConfiguration
{
    public string VendorID { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string AssignedEmployeeID { get; set; } = string.Empty;
    public string AssignedEmployeeName { get; set; } = string.Empty;
    public string InventoryApiUrl { get; set; } = string.Empty;

    public static WindowsAgentConfiguration Load()
    {
        const string configPath =
            @"C:\ProgramData\AHTAgent\config.json";

        var configuration =
            new WindowsAgentConfiguration();

        var environmentApiUrl =
            Environment.GetEnvironmentVariable(
                "ASSET_MANAGER_API_URL",
                EnvironmentVariableTarget.Machine);

        if (!string.IsNullOrWhiteSpace(environmentApiUrl))
        {
            configuration.InventoryApiUrl =
                environmentApiUrl.Trim();
        }

        if (!OperatingSystem.IsWindows())
            return configuration;

        if (!File.Exists(configPath))
            return configuration;

        try
        {
            var json =
                File.ReadAllText(configPath);

            var fileConfiguration =
                JsonSerializer.Deserialize<WindowsAgentConfiguration>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (fileConfiguration != null)
            {
                configuration.VendorID =
                    fileConfiguration.VendorID;

                configuration.VendorName =
                    fileConfiguration.VendorName;

                configuration.AssignedEmployeeID =
                    fileConfiguration.AssignedEmployeeID;

                configuration.AssignedEmployeeName =
                    fileConfiguration.AssignedEmployeeName;

                if (string.IsNullOrWhiteSpace(
                        configuration.InventoryApiUrl) &&
                    !string.IsNullOrWhiteSpace(
                        fileConfiguration.InventoryApiUrl))
                {
                    configuration.InventoryApiUrl =
                        fileConfiguration.InventoryApiUrl.Trim();
                }
            }
        }
        catch
        {
            // Keep the environment configuration if the file cannot be read.
        }

        return configuration;
    }
}
