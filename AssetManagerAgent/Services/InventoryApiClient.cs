using System.Net.Http.Json;
using AssetManagerAgent.Core.History;
using AssetManagerAgent.Core.Models;
using AssetManagerAgent.Services.Health;
using AssetManagerAgent.Services.Configuration;

namespace AssetManagerAgent.Services;

public class InventoryApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;

    public InventoryApiClient(string apiUrl)
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        _apiUrl = apiUrl;
    }

    public async Task<bool> SendAssetAsync(
        Asset asset,
        AssetHealth health,
        List<ComponentChange> changes)
    {
        if (string.IsNullOrWhiteSpace(_apiUrl))
            return false;

        try
        {
            var configuration = WindowsAgentConfiguration.Load();
            Console.WriteLine(
    $"CONFIG CHECK: VendorID='{configuration.VendorID}'");

Console.WriteLine(
    $"CONFIG CHECK: VendorName='{configuration.VendorName}'");

            var request = new InventoryApiRequest
            {
                Asset = asset,
                Health = health,
                Changes = changes,
                VendorID = configuration.VendorID,
                VendorName = configuration.VendorName
            };

            using var response = await _httpClient.PostAsJsonAsync(
                _apiUrl,
                request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"API ERROR: HTTP {(int)response.StatusCode} " +
                    $"{response.ReasonPhrase}");

                return false;
            }

            Console.WriteLine(
                $"Vendor sent: {configuration.VendorName} " +
                $"({configuration.VendorID})");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"API ERROR: {ex.GetType().Name} - {ex.Message}");

            return false;
        }
    }
}