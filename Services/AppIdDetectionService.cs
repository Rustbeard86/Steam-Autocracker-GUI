using System.Data;
using APPID.Models;
using APPID.Services.Interfaces;
using APPID.Utilities.Network;
using APPID.Utilities.Steam;
using Newtonsoft.Json;

namespace APPID.Services;

/// <summary>
///     Implementation of AppID detection service for Steam games.
///     Provides multiple detection methods with fallbacks and caching.
/// </summary>
public sealed class AppIdDetectionService : IAppIdDetectionService
{
    private readonly ISteamCacheManager _cache;
    private readonly IFileSystemService _fileSystem;
    private readonly IManifestParsingService _manifestParsing;

    public AppIdDetectionService(
        IFileSystemService fileSystem,
        IManifestParsingService manifestParsing,
        ISteamCacheManager cache)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _manifestParsing = manifestParsing ?? throw new ArgumentNullException(nameof(manifestParsing));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public string? DetectAppId(string gamePath)
    {
        try
        {
            // Normalize path
            gamePath = gamePath.TrimEnd('\\', '/');

            // Try manifest first - walk up directory tree to find steamapps folder
            string? steamappsPath = SteamFolderStructureHelper.FindSteamappsDirectory(gamePath);
            if (steamappsPath != null)
            {
                string gameFolderName = SteamFolderStructureHelper.GetInstallDirectoryName(gamePath);
                var manifestFiles = _manifestParsing.FindManifestFiles(steamappsPath);

                foreach (var manifestPath in manifestFiles)
                {
                    var manifestInfo = _manifestParsing.ParseManifest(manifestPath);
                    if (manifestInfo != null &&
                        string.Equals(manifestInfo.InstallDir, gameFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        return manifestInfo.AppId;
                    }
                }
            }

            // Try steam_appid.txt in game folder
            var appIdFile = Path.Combine(gamePath, "steam_appid.txt");
            if (_fileSystem.FileExists(appIdFile))
            {
                string content = File.ReadAllText(appIdFile).Trim();
                if (!string.IsNullOrEmpty(content) && content.All(char.IsDigit))
                {
                    return content;
                }
            }

            // Try steam_settings folder
            var steamSettingsAppId = Path.Combine(gamePath, "steam_settings", "steam_appid.txt");
            if (_fileSystem.FileExists(steamSettingsAppId))
            {
                string content = File.ReadAllText(steamSettingsAppId).Trim();
                if (!string.IsNullOrEmpty(content) && content.All(char.IsDigit))
                {
                    return content;
                }
            }

            // Try to find appmanifest directly by scanning steamapps folder (backup method)
            if (steamappsPath != null)
            {
                string gameFolderName = SteamFolderStructureHelper.GetInstallDirectoryName(gamePath);
                var acfFiles = _fileSystem.GetFiles(steamappsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
                foreach (var acf in acfFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(acf);
                        // Look for installdir that matches (case insensitive, partial match)
                        if (content.IndexOf($"\"{gameFolderName}\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Extract appid from this file using centralized parser
                            var manifest = AcfFileParser.ParseFlat(content);
                            if (manifest.TryGetValue("appid", out var appId))
                            {
                                return appId;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Try Steam Store Search API first (better fuzzy matching)
            string folderName = Path.GetFileName(gamePath);
            string? apiAppId = SearchSteamStoreApi(folderName);
            if (!string.IsNullOrEmpty(apiAppId))
            {
                return apiAppId;
            }

            // Fall back to local database search
            string? foundAppId = SearchSteamDbByFolderName(folderName);
            if (!string.IsNullOrEmpty(foundAppId))
            {
                return foundAppId;
            }
        }
        catch { }

        return null;
    }

    public string? SearchSteamStoreApi(string gameName)
    {
        try
        {
            // Check cache first
            string cacheKey = $"store_search_{gameName.ToLowerInvariant()}";
            var cachedResult = _cache.Get<string>(cacheKey);
            if (cachedResult != null)
            {
                Debug.WriteLine($"[STEAM API] Cache hit for '{gameName}'");
                return cachedResult;
            }

            // Clean up the search term
            string searchTerm = gameName
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .Trim();

            // Build URL using constants
            string url = SteamApiConstants.BuildStoreSearchUrl(searchTerm);

            // Use HttpClient instead of obsolete WebClient
            using var client = HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Use synchronous call since this method signature is sync
            var task = client.GetStringAsync(url);
            string json = task.GetAwaiter().GetResult();

            // Parse JSON response
            var response = JsonConvert.DeserializeObject<SteamStoreSearchResponse>(json);

            if (response?.Items == null || response.Items.Count == 0)
            {
                return null;
            }

            // Filter out unwanted results using centralized constant
            var filteredItems = response.Items.Where(item =>
            {
                if (item.Type != SteamApiConstants.AppTypeGame)
                {
                    return false;
                }

                string nameLower = item.Name?.ToLower() ?? "";

                // Use centralized exclusion check
                return !SteamApiConstants.ContainsExcludedKeyword(nameLower);
            }).ToList();

            if (filteredItems.Count == 0)
            {
                // If all results were filtered, try the first app-type result
                var firstApp = response.Items.FirstOrDefault(i => i.Type == SteamApiConstants.AppTypeGame);
                var appId = firstApp?.Id.ToString();

                // Cache the result
                if (appId != null)
                {
                    _cache.Set(cacheKey, appId, TimeSpan.FromMinutes(SteamApiConstants.CacheDurationMinutes));
                }

                return appId;
            }

            // Return the first (best) match
            var resultAppId = filteredItems[0].Id.ToString();

            // Cache the result
            _cache.Set(cacheKey, resultAppId, TimeSpan.FromMinutes(SteamApiConstants.CacheDurationMinutes));

            return resultAppId;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[STEAM API] Search error for '{gameName}': {ex.Message}");
        }

        return null;
    }

    public string? SearchSteamDbByFolderName(string folderName)
    {
        try
        {
            var dataGen = new DataTableGeneration();
            var table = dataGen.DataTableToGenerate;
            if (table == null)
            {
                return null;
            }

            // Clean folder name for matching
            string searchName = folderName.Replace("_", " ").Replace("-", " ").Trim();

            // Try exact match first (case insensitive)
            foreach (DataRow row in table.Rows)
            {
                string gameName = row[0]?.ToString() ?? "";
                if (gameName.Equals(searchName, StringComparison.OrdinalIgnoreCase) ||
                    gameName.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return row[1]?.ToString();
                }
            }

            // Try contains match - folder name in game name or vice versa
            foreach (DataRow row in table.Rows)
            {
                string gameName = row[0]?.ToString() ?? "";
                // Normalize both for comparison
                string normalizedGameName = gameName.Replace(":", "").Replace("-", " ").Replace("_", " ").ToLower();
                string normalizedFolderName = folderName.Replace("_", " ").Replace("-", " ").ToLower();

                if (normalizedGameName.Equals(normalizedFolderName) ||
                    normalizedGameName.Replace(" ", "").Equals(normalizedFolderName.Replace(" ", "")))
                {
                    return row[1]?.ToString();
                }
            }
        }
        catch { }

        return null;
    }
}
