using System.Data;
using System.Text.RegularExpressions;
using APPID.Models;
using APPID.Services.Interfaces;
using APPID.Utilities.Network;
using APPID.Utilities.Steam;
using Newtonsoft.Json;

namespace APPID.Services;

/// <summary>
///     Implementation of AppID detection service for Steam games.
///     Provides multiple detection methods with fallbacks.
/// </summary>
public sealed class AppIdDetectionService(IFileSystemService fileSystem, IManifestParsingService manifestParsing)
    : IAppIdDetectionService
{
    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IManifestParsingService _manifestParsing =
        manifestParsing ?? throw new ArgumentNullException(nameof(manifestParsing));

    public string? DetectAppId(string gamePath)
    {
        try
        {
            // Normalize path
            gamePath = gamePath.TrimEnd('\\', '/');

            // Try manifest first - walk up directory tree to find steamapps folder
            string? steamappsPath = FindSteamappsPath(gamePath);
            if (steamappsPath != null)
            {
                string gameFolderName = Path.GetFileName(gamePath);
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
                string gameFolderName = Path.GetFileName(gamePath);
                var acfFiles = _fileSystem.GetFiles(steamappsPath, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
                foreach (var acf in acfFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(acf);
                        // Look for installdir that matches (case insensitive, partial match)
                        if (content.IndexOf($"\"{gameFolderName}\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Extract appid from this file
                            var match = Regex.Match(content, @"""appid""\s+""(\d+)""");
                            if (match.Success)
                            {
                                return match.Groups[1].Value;
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
            // Clean up the search term
            string searchTerm = gameName
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace(".", " ")
                .Trim();

            // URL encode the search term
            string encodedTerm = Uri.EscapeDataString(searchTerm);
            string url = $"https://store.steampowered.com/api/storesearch?term={encodedTerm}&cc=us&l=en-us";

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

            // Filter out unwanted results (soundtracks, DLC, demos, etc.)
            var filteredItems = response.Items.Where(item =>
            {
                if (item.Type != "app")
                {
                    return false;
                }

                string nameLower = item.Name?.ToLower() ?? "";

                // Filter out common non-game content
                if (nameLower.Contains("soundtrack"))
                {
                    return false;
                }

                if (nameLower.Contains("dlc"))
                {
                    return false;
                }

                if (nameLower.Contains("demo"))
                {
                    return false;
                }

                if (nameLower.Contains("beta"))
                {
                    return false;
                }

                if (nameLower.Contains("test"))
                {
                    return false;
                }

                if (nameLower.Contains("server"))
                {
                    return false;
                }

                if (nameLower.Contains("playtest"))
                {
                    return false;
                }

                if (nameLower.Contains("dedicated"))
                {
                    return false;
                }

                if (nameLower.Contains("sdk"))
                {
                    return false;
                }

                if (nameLower.Contains("editor"))
                {
                    return false;
                }

                if (nameLower.Contains("tool"))
                {
                    return false;
                }

                if (nameLower.Contains("artbook"))
                {
                    return false;
                }

                if (nameLower.Contains("art book"))
                {
                    return false;
                }

                if (nameLower.Contains("original score"))
                {
                    return false;
                }

                if (nameLower.Contains("ost"))
                {
                    return false;
                }

                return true;
            }).ToList();

            if (filteredItems.Count == 0)
            {
                // If all results were filtered, try the first app-type result
                var firstApp = response.Items.FirstOrDefault(i => i.Type == "app");
                return firstApp?.Id.ToString();
            }

            // Return the first (best) match
            return filteredItems[0].Id.ToString();
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

    /// <summary>
    ///     Walks up the directory tree from the game path to find the steamapps folder.
    /// </summary>
    private string? FindSteamappsPath(string gamePath)
    {
        var dir = new DirectoryInfo(gamePath);

        while (dir != null)
        {
            if (dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return dir.FullName;
            }

            if (dir.Name.Equals("common", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
            {
                return dir.Parent.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
