using APPID.Services.Interfaces;
using APPID.Utilities.Network;
using Newtonsoft.Json.Linq;

namespace APPID.Services;

/// <summary>
///     Implementation of DLC service for fetching DLC information from Steam API.
/// </summary>
public sealed class DlcService : IDlcService
{
    public async Task FetchDlcInfoAsync(string appId, string outputFolder, Action<string, Color>? statusCallback = null)
    {
        // Use HttpClientFactory instead of creating new HttpClient directly
        using var httpClient = HttpClientFactory.CreateClient(true, TimeSpan.FromSeconds(30));

        try
        {
            // Get app details from Steam Store API
            var response =
                await httpClient.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}");
            var json = JObject.Parse(response);

            var appData = json[appId]?["data"];
            if (appData == null || json[appId]?["success"]?.Value<bool>() != true)
            {
                statusCallback?.Invoke("No DLC info available for this game", Color.Yellow);
                return;
            }

            if (appData["dlc"] is not JArray dlcArray || dlcArray.Count == 0)
            {
                statusCallback?.Invoke("No DLCs found for this game", Color.Yellow);
                return;
            }

            statusCallback?.Invoke($"Found {dlcArray.Count} DLCs, fetching names...", Color.Cyan);

            var dlcLines = new List<string>();
            int successCount = 0;

            foreach (var dlcId in dlcArray)
            {
                try
                {
                    var dlcResponse =
                        await httpClient.GetStringAsync(
                            $"https://store.steampowered.com/api/appdetails?appids={dlcId}");
                    var dlcJson = JObject.Parse(dlcResponse);

                    var dlcData = dlcJson[dlcId.ToString()]?["data"];
                    if (dlcData != null && dlcJson[dlcId.ToString()]?["success"]?.Value<bool>() == true)
                    {
                        string dlcName = dlcData["name"]?.Value<string>() ?? "Unknown";
                        dlcLines.Add($"{dlcId}={dlcName}");
                        successCount++;
                    }

                    // Rate limit to avoid Steam throttling
                    await Task.Delay(100);
                }
                catch
                {
                    // Skip DLCs that fail to fetch
                    dlcLines.Add($"{dlcId}=DLC_{dlcId}");
                }
            }

            // Write DLC.txt
            string dlcPath = Path.Combine(outputFolder, "DLC.txt");
            File.WriteAllLines(dlcPath, dlcLines);

            statusCallback?.Invoke($"Saved {successCount}/{dlcArray.Count} DLC entries!", Color.Green);
        }
        catch (TaskCanceledException)
        {
            throw new Exception("Request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Network error: {ex.Message}");
        }
    }
}
