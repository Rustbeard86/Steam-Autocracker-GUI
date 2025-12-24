using System.Text.RegularExpressions;
using APPID.Services.Interfaces;
using APPID.Utilities.Steam;

namespace APPID.Services;

/// <summary>
///     Implementation of manifest parsing service for Steam ACF files.
///     Delegates to SteamManifestParser for actual parsing logic.
/// </summary>
public sealed class ManifestParsingService(IFileSystemService fileSystem) : IManifestParsingService
{
    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public ManifestInfo? ParseManifest(string manifestPath)
    {
        try
        {
            if (!_fileSystem.FileExists(manifestPath))
            {
                return null;
            }

            var content = _fileSystem.ReadAllTextAsync(manifestPath).GetAwaiter().GetResult();
            var manifest = ParseAcfFile(content);

            if (!manifest.ContainsKey("appid") || !manifest.ContainsKey("installdir"))
            {
                return null;
            }

            return new ManifestInfo
            {
                AppId = manifest["appid"],
                InstallDir = manifest["installdir"],
                Name = manifest.TryGetValue("name", out string? value) ? value : manifest["installdir"]
            };
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Failed to parse manifest: {manifestPath}", ex);
            return null;
        }
    }

    public List<string> FindManifestFiles(string path)
    {
        var manifestFiles = new List<string>();

        if (!_fileSystem.DirectoryExists(path))
        {
            return manifestFiles;
        }

        try
        {
            manifestFiles.AddRange(_fileSystem.GetFiles(path, "appmanifest_*.acf", SearchOption.TopDirectoryOnly));
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Failed to find manifest files in: {path}", ex);
        }

        return manifestFiles;
    }

    public List<string> DetectSteamLibraryFolders()
    {
        var paths = new List<string>();

        // Default Steam installation paths
        var defaultPaths = new[]
        {
            @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        // Add default paths that exist
        foreach (var path in defaultPaths)
        {
            if (_fileSystem.DirectoryExists(path) && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        // Try to find additional library folders from Steam's libraryfolders.vdf
        foreach (var steamPath in paths.ToList())
        {
            var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (_fileSystem.FileExists(vdfPath))
            {
                try
                {
                    var vdfContent = _fileSystem.ReadAllTextAsync(vdfPath).GetAwaiter().GetResult();
                    // Look for path entries in the VDF file
                    var pathMatches = Regex.Matches(vdfContent, @"""path""\s+""([^""]*)""");

                    foreach (Match match in pathMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            if (_fileSystem.DirectoryExists(libPath) && !paths.Contains(libPath))
                            {
                                paths.Add(libPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"Failed to parse libraryfolders.vdf: {vdfPath}", ex);
                }
            }
        }

        return paths;
    }

    public (string appId, string gameName, long sizeOnDisk)? GetAppIdFromManifest(string droppedPath)
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.GetAppIdFromManifest(droppedPath);
    }

    public (string gameName, string appId, long sizeOnDisk, string buildId, long lastUpdated,
        Dictionary<string, (string manifest, long size)> depots)? GetFullManifestInfo(string gameInstallPath)
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.GetFullManifestInfo(gameInstallPath);
    }

    public List<(string appId, string gameName, string installPath)> FindAllInstalledGames()
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.FindAllInstalledGames();
    }

    public string? GetAppIdFromManifestFile(string acfFilePath)
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.GetAppIdFromManifestFile(acfFilePath);
    }

    public bool VerifyGameSize(string gamePath, long manifestSize, long toleranceBytes = 10485760)
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.VerifyGameSize(gamePath, manifestSize, toleranceBytes);
    }

    public string DetectGamePlatform(string gamePath)
    {
        // Delegate to static SteamManifestParser for now
        return SteamManifestParser.DetectGamePlatform(gamePath);
    }

    private static Dictionary<string, string> ParseAcfFile(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Regular expressions for parsing VDF/ACF format
        var keyValuePattern = @"""(\w+)""\s+""([^""]*)""";
        var matches = Regex.Matches(content, keyValuePattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // Only store the first occurrence of each key (ignores nested structures)
                result.TryAdd(key, value);
            }
        }

        return result;
    }
}
