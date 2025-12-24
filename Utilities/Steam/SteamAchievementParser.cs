using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace APPID.Utilities.Steam
{
    namespace SteamTools
    {
        namespace SteamTools
        {
            public class SteamAchievementParser
            {
                private readonly HttpClient _httpClient;

                public SteamAchievementParser(HttpClient httpClient, string config)
                {
                    _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

                    // CONFIGURE HEADERS
                    // 1. Set the User-Agent so the Worker knows it's us
                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GbeTool/1.0");

                    // 2. Set the Custom Auth Header (The Secret)
                    if (!string.IsNullOrEmpty(config))
                    {
                        // Remove if it already exists to prevent duplicates
                        if (_httpClient.DefaultRequestHeaders.Contains("X-Gbe-Auth"))
                        {
                            _httpClient.DefaultRequestHeaders.Remove("X-Gbe-Auth");
                        }

                        _httpClient.DefaultRequestHeaders.Add("X-Gbe-Auth", config);
                    }
                }

                /// <summary>
                ///     Fetches achievement definitions from the proxy/Steam API, downloads icons, and generates achievements.json.
                /// </summary>
                /// <param name="baseUrl">The base URL (e.g., "https://api.yourdomain.com" or direct Steam URL).</param>
                /// <param name="apiKey">Your Steam Web API Key (Optional).</param>
                /// <param name="appId">The Steam AppID of the game.</param>
                /// <param name="outputDirectory">The root folder where steam_settings should be created/updated.</param>
                /// <returns>True if successful, False otherwise.</returns>
                public async Task<bool> GenerateAchievementsFileAsync(string baseUrl, string apiKey, string appId,
                    string outputDirectory)
                {
                    string settingsDir = Path.Combine(outputDirectory, "steam_settings");
                    string imagesDir = Path.Combine(settingsDir, "achievement_images");
                    string jsonPath = Path.Combine(settingsDir, "achievements.json");

                    try
                    {
                        var achievements = await FetchSteamSchema(baseUrl, appId, apiKey);

                        if (achievements == null || achievements.Count == 0)
                        {
                            return false;
                        }

                        Directory.CreateDirectory(imagesDir);

                        var goldbergList = new List<GoldbergAchievement>();

                        // Parallel download processing can speed this up significantly
                        var tasks = new List<Task>();

                        foreach (var ach in achievements)
                        {
                            string iconName = $"{ach.Name}_unlocked.jpg";
                            string iconGrayName = $"{ach.Name}_locked.jpg";

                            // Queue image downloads
                            tasks.Add(DownloadImage(ach.IconUrl, Path.Combine(imagesDir, iconName)));
                            tasks.Add(DownloadImage(ach.IconGrayUrl, Path.Combine(imagesDir, iconGrayName)));

                            goldbergList.Add(new GoldbergAchievement
                            {
                                Name = ach.Name,
                                DisplayName = ach.DisplayName,
                                Description = ach.Description ?? string.Empty,
                                Icon = iconName,
                                IconGray = iconGrayName,
                                Hidden = ach.Hidden
                            });
                        }

                        // Wait for all images to download
                        await Task.WhenAll(tasks);

                        // Serialize and Save JSON
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string jsonOutput = JsonSerializer.Serialize(goldbergList, options);

                        await File.WriteAllTextAsync(jsonPath, jsonOutput);

                        return true;
                    }
                    catch (Exception ex)
                    {
                        return false;
                    }
                }

                private async Task<List<SteamAchievementDef>> FetchSteamSchema(string baseUrl, string appId,
                    string apiKey)
                {
                    // Build URL dynamically based on whether an API key is provided (Direct vs Proxy)
                    var query = HttpUtility.ParseQueryString(string.Empty);
                    query["appid"] = appId;
                    query["l"] = "en";

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        query["key"] = apiKey;
                    }

                    string requestUrl = $"{baseUrl.TrimEnd('/')}/ISteamUserStats/GetSchemaForGame/v2/?{query}";

                    using var response = await _httpClient.GetAsync(requestUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string json = await response.Content.ReadAsStringAsync();

                    try
                    {
                        var root = JsonSerializer.Deserialize<SteamRootObject>(json);
                        return root?.Game?.AvailableGameStats?.Achievements;
                    }
                    catch (JsonException ex)
                    {
                        return null;
                    }
                }

                private async Task DownloadImage(string url, string localPath)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        return;
                    }

                    if (File.Exists(localPath))
                    {
                        return;
                    }

                    try
                    {
                        var data = await _httpClient.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(localPath, data);
                    }
                    catch (Exception ex)
                    {
                        // ignored
                    }
                }

                // --- Data Models ---

                private class SteamRootObject
                {
                    [JsonPropertyName("game")] public SteamGame Game { get; set; }
                }

                private class SteamGame
                {
                    [JsonPropertyName("availableGameStats")]
                    public SteamStats AvailableGameStats { get; set; }
                }

                private class SteamStats
                {
                    [JsonPropertyName("achievements")] public List<SteamAchievementDef> Achievements { get; set; }
                }

                private class SteamAchievementDef
                {
                    [JsonPropertyName("name")] public string Name { get; set; }

                    [JsonPropertyName("displayName")] public string DisplayName { get; set; }

                    [JsonPropertyName("description")] public string Description { get; set; }

                    [JsonPropertyName("icon")] public string IconUrl { get; set; }

                    [JsonPropertyName("icongray")] public string IconGrayUrl { get; set; }

                    [JsonPropertyName("hidden")] public int Hidden { get; set; }
                }

                private class GoldbergAchievement
                {
                    [JsonPropertyName("name")] public string Name { get; set; }

                    [JsonPropertyName("displayName")] public string DisplayName { get; set; }

                    [JsonPropertyName("description")] public string Description { get; set; }

                    [JsonPropertyName("icon")] public string Icon { get; set; }

                    [JsonPropertyName("icon_gray")] public string IconGray { get; set; }

                    [JsonPropertyName("hidden")] public int Hidden { get; set; }
                }
            }
        }
    }
}
