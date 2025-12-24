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
                        LogHelper.Log("[ACHIEVEMENTS] Auth header configured");
                    }
                    else
                    {
                        LogHelper.Log("[ACHIEVEMENTS] WARNING: No auth config provided");
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
                        LogHelper.Log($"[ACHIEVEMENTS] Starting parse for AppID: {appId}");
                        LogHelper.Log($"[ACHIEVEMENTS] Using base URL: {baseUrl}");
                        LogHelper.Log($"[ACHIEVEMENTS] Output directory: {outputDirectory}");

                        var achievements = await FetchSteamSchema(baseUrl, appId, apiKey);

                        if (achievements == null || achievements.Count == 0)
                        {
                            LogHelper.Log($"[ACHIEVEMENTS] No achievements returned from API for AppID: {appId}");
                            return false;
                        }

                        LogHelper.Log($"[ACHIEVEMENTS] Found {achievements.Count} achievements for AppID: {appId}");
                        LogHelper.Log($"[ACHIEVEMENTS] Creating output directories at: {settingsDir}");

                        Directory.CreateDirectory(imagesDir);

                        var goldbergList = new List<GoldbergAchievement>();

                        // Parallel download processing can speed this up significantly
                        var tasks = new List<Task>();
                        int iconCount = 0;

                        foreach (var ach in achievements)
                        {
                            string iconName = $"{ach.Name}_unlocked.jpg";
                            string iconGrayName = $"{ach.Name}_locked.jpg";

                            // Queue image downloads
                            tasks.Add(DownloadImage(ach.IconUrl, Path.Combine(imagesDir, iconName)));
                            tasks.Add(DownloadImage(ach.IconGrayUrl, Path.Combine(imagesDir, iconGrayName)));
                            iconCount += 2;

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

                        LogHelper.Log($"[ACHIEVEMENTS] Downloading {iconCount} achievement icons in parallel...");

                        // Wait for all images to download
                        await Task.WhenAll(tasks);

                        LogHelper.Log("[ACHIEVEMENTS] All icons downloaded successfully");

                        // Serialize and Save JSON
                        var options = new JsonSerializerOptions { WriteIndented = true };
                        string jsonOutput = JsonSerializer.Serialize(goldbergList, options);

                        await File.WriteAllTextAsync(jsonPath, jsonOutput);

                        LogHelper.Log($"[ACHIEVEMENTS] Successfully generated achievements.json at: {jsonPath}");
                        LogHelper.Log($"[ACHIEVEMENTS] Total achievements processed: {goldbergList.Count}");

                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"[ACHIEVEMENTS] Critical error parsing achievements for AppID: {appId}",
                            ex);
                        return false;
                    }
                }

                private async Task<List<SteamAchievementDef>> FetchSteamSchema(string baseUrl, string appId,
                    string apiKey)
                {
                    try
                    {
                        // Build URL dynamically based on whether an API key is provided (Direct vs Proxy)
                        var query = HttpUtility.ParseQueryString(string.Empty);
                        query["appid"] = appId;
                        query["l"] = "en";

                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            query["key"] = apiKey;
                            LogHelper.Log("[ACHIEVEMENTS] Using Steam API key for direct access");
                        }
                        else
                        {
                            LogHelper.Log("[ACHIEVEMENTS] Using proxy mode (no API key)");
                        }

                        string requestUrl = $"{baseUrl.TrimEnd('/')}/ISteamUserStats/GetSchemaForGame/v2/?{query}";

                        // CRITICAL FIX: Prevent String.Replace("") crash when apiKey is null
                        string maskedUrl = !string.IsNullOrEmpty(apiKey)
                            ? requestUrl.Replace(apiKey, "***")
                            : requestUrl;

                        LogHelper.LogApi("Steam GetSchemaForGame", $"Requesting: {maskedUrl}");

                        using var response = await _httpClient.GetAsync(requestUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            LogHelper.LogNetwork(
                                $"[ACHIEVEMENTS] API returned HTTP {(int)response.StatusCode} {response.StatusCode} for AppID: {appId}");

                            // Try to get error details
                            try
                            {
                                string errorBody = await response.Content.ReadAsStringAsync();
                                if (!string.IsNullOrEmpty(errorBody) && errorBody.Length < 500)
                                {
                                    LogHelper.Log($"[ACHIEVEMENTS] Error response: {errorBody}");
                                }
                            }
                            catch { }

                            return null;
                        }

                        LogHelper.LogApi("Steam GetSchemaForGame", $"Success (HTTP {(int)response.StatusCode})");

                        string json = await response.Content.ReadAsStringAsync();
                        LogHelper.Log($"[ACHIEVEMENTS] Received response, size: {json.Length:N0} bytes");

                        var root = JsonSerializer.Deserialize<SteamRootObject>(json);

                        if (root?.Game?.AvailableGameStats?.Achievements != null)
                        {
                            int count = root.Game.AvailableGameStats.Achievements.Count;
                            LogHelper.Log($"[ACHIEVEMENTS] Successfully parsed {count} achievement definitions");
                            return root.Game.AvailableGameStats.Achievements;
                        }

                        LogHelper.Log(
                            $"[ACHIEVEMENTS] Response structure invalid or no achievements in API response for AppID: {appId}");
                        LogHelper.Log(
                            $"[ACHIEVEMENTS] Response preview: {json.Substring(0, Math.Min(200, json.Length))}...");
                        return null;
                    }
                    catch (JsonException ex)
                    {
                        LogHelper.LogError("[ACHIEVEMENTS] Failed to deserialize Steam API JSON response", ex);
                        return null;
                    }
                    catch (HttpRequestException ex)
                    {
                        LogHelper.LogError($"[ACHIEVEMENTS] Network error fetching schema for AppID: {appId}", ex);
                        return null;
                    }
                    catch (TaskCanceledException ex)
                    {
                        LogHelper.LogError($"[ACHIEVEMENTS] Request timeout for AppID: {appId}", ex);
                        return null;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"[ACHIEVEMENTS] Unexpected error in FetchSteamSchema for AppID: {appId}",
                            ex);
                        return null;
                    }
                }

                private async Task DownloadImage(string url, string localPath)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        LogHelper.Log($"[ACHIEVEMENTS] Skipping empty URL for: {Path.GetFileName(localPath)}");
                        return;
                    }

                    if (File.Exists(localPath))
                    {
                        // Silent skip for existing files to reduce log spam
                        return;
                    }

                    try
                    {
                        var data = await _httpClient.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(localPath, data);

                        // Only log downloads in debug mode to reduce log spam
                        if (data.Length > 0)
                        {
                            Debug.WriteLine(
                                $"[ACHIEVEMENTS] Downloaded: {Path.GetFileName(localPath)} ({data.Length:N0} bytes)");
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        LogHelper.LogNetwork($"[ACHIEVEMENTS] Failed to download icon from: {url} - {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogError($"[ACHIEVEMENTS] Error downloading icon: {Path.GetFileName(localPath)}", ex);
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
