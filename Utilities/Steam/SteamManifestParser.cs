using System.Text.RegularExpressions;

namespace APPID.Utilities.Steam;

/// <summary>
///     Parses Steam ACF manifest files to automatically detect AppIDs
/// </summary>
public static class SteamManifestParser
{
    /// <summary>
    ///     Attempts to find AppID from Steam manifest files when a folder is dropped
    /// </summary>
    /// <param name="droppedPath">The path that was dropped onto the application</param>
    /// <returns>Tuple of (AppID, GameName, SizeOnDisk) or null if not found</returns>
    public static (string appId, string gameName, long sizeOnDisk)? GetAppIdFromManifest(string droppedPath)
    {
        // Check if this path contains "steamapps" - indicating it's from a Steam library
        if (droppedPath.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Debug.WriteLine("[MANIFEST] Path doesn't contain 'steamapps', skipping manifest check");
            return null;
        }

        // Get the game folder name (last part of the path)
        string gameFolderName = Path.GetFileName(droppedPath.TrimEnd('\\', '/'));
        Debug.WriteLine($"[MANIFEST] Checking for game folder: {gameFolderName}");

        // Find the steamapps directory
        string steamappsPath = GetSteamappsPath(droppedPath);
        if (string.IsNullOrEmpty(steamappsPath))
        {
            Debug.WriteLine("[MANIFEST] Could not find steamapps directory");
            return null;
        }

        Debug.WriteLine($"[MANIFEST] Steamapps directory: {steamappsPath}");

        // Look for all appmanifest_*.acf files
        var acfFiles = Directory.GetFiles(steamappsPath, "appmanifest_*.acf");
        Debug.WriteLine($"[MANIFEST] Found {acfFiles.Length} ACF files");

        foreach (var acfFile in acfFiles)
        {
            try
            {
                string content = File.ReadAllText(acfFile);
                var manifest = ParseAcfFile(content);

                // Check if this manifest's installdir matches our game folder
                if (manifest.ContainsKey("installdir") &&
                    string.Equals(manifest["installdir"], gameFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    string appId = manifest.GetValueOrDefault("appid");
                    string gameName = manifest.GetValueOrDefault("name", gameFolderName);
                    long sizeOnDisk = 0;

                    if (manifest.TryGetValue("SizeOnDisk", out string? value2))
                    {
                        long.TryParse(value2, out sizeOnDisk);
                    }

                    Debug.WriteLine("[MANIFEST] ✅ Found match!");
                    Debug.WriteLine($"[MANIFEST] AppID: {appId}");
                    Debug.WriteLine($"[MANIFEST] Name: {gameName}");
                    Debug.WriteLine($"[MANIFEST] Size: {sizeOnDisk / (1024 * 1024)} MB");

                    return (appId, gameName, sizeOnDisk);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MANIFEST] Error parsing {acfFile}: {ex.Message}");
            }
        }

        Debug.WriteLine("[MANIFEST] No matching manifest found");
        return null;
    }

    /// <summary>
    ///     Gets AppID directly from a known manifest file path
    /// </summary>
    public static string GetAppIdFromManifestFile(string acfFilePath)
    {
        try
        {
            string content = File.ReadAllText(acfFilePath);
            var manifest = ParseAcfFile(content);
            return manifest.GetValueOrDefault("appid");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Verifies if the actual folder size matches the manifest size (within tolerance)
    /// </summary>
    public static bool
        VerifyGameSize(string gamePath, long manifestSize, long toleranceBytes = 10485760) // 10MB tolerance
    {
        long actualSize = GetDirectorySize(gamePath);
        long difference = Math.Abs(actualSize - manifestSize);

        Debug.WriteLine("[MANIFEST] Size verification:");
        Debug.WriteLine($"[MANIFEST] Manifest size: {manifestSize / (1024 * 1024)} MB");
        Debug.WriteLine($"[MANIFEST] Actual size: {actualSize / (1024 * 1024)} MB");
        Debug.WriteLine($"[MANIFEST] Difference: {difference / (1024 * 1024)} MB");

        return difference <= toleranceBytes;
    }

    /// <summary>
    ///     Finds the steamapps directory from a given path
    /// </summary>
    private static string GetSteamappsPath(string path)
    {
        // Navigate up the directory tree to find steamapps
        var dir = new DirectoryInfo(path);

        while (dir != null)
        {
            if (dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return dir.FullName;
            }

            // Check if current directory contains a steamapps folder
            var steamappsSubdir = dir.GetDirectories("steamapps", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (steamappsSubdir != null)
            {
                return steamappsSubdir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    ///     Parses an ACF file into a dictionary of key-value pairs
    /// </summary>
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

    /// <summary>
    ///     Gets the total size of a directory in bytes
    /// </summary>
    private static long GetDirectorySize(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     Finds all installed Steam games by scanning manifest files
    /// </summary>
    public static List<(string appId, string gameName, string installPath)> FindAllInstalledGames()
    {
        var games = new List<(string, string, string)>();
        var steamPaths = GetAllSteamLibraryPaths();

        foreach (var steamPath in steamPaths)
        {
            var steamappsPath = Path.Combine(steamPath, "steamapps");
            if (!Directory.Exists(steamappsPath))
            {
                continue;
            }

            var acfFiles = Directory.GetFiles(steamappsPath, "appmanifest_*.acf");

            foreach (var acfFile in acfFiles)
            {
                try
                {
                    string content = File.ReadAllText(acfFile);
                    var manifest = ParseAcfFile(content);

                    if (manifest.ContainsKey("appid") && manifest.ContainsKey("name") &&
                        manifest.TryGetValue("installdir", out string? value))
                    {
                        string installPath = Path.Combine(steamappsPath, "common", value);
                        if (Directory.Exists(installPath))
                        {
                            games.Add((manifest["appid"], manifest["name"], installPath));
                        }
                    }
                }
                catch { }
            }
        }

        return games;
    }

    /// <summary>
    ///     Gets all Steam library paths from the system
    /// </summary>
    private static List<string> GetAllSteamLibraryPaths()
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
            if (Directory.Exists(path) && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        // Try to find additional library folders from Steam's libraryfolders.vdf
        foreach (var steamPath in paths.ToList())
        {
            var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    string vdfContent = File.ReadAllText(vdfPath);
                    // Look for path entries in the VDF file
                    var pathMatches = Regex.Matches(vdfContent, @"""path""\s+""([^""]*)""");

                    foreach (Match match in pathMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            if (Directory.Exists(libPath) && !paths.Contains(libPath))
                            {
                                paths.Add(libPath);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        return paths;
    }

    /// <summary>
    ///     Gets full manifest info including build ID, depots, and timestamps for cs.rin.ru format
    /// </summary>
    public static (string gameName, string appId, long sizeOnDisk, string buildId, long lastUpdated,
        Dictionary<string, (string manifest, long size)> depots)? GetFullManifestInfo(string gameInstallPath)
    {
        var basicInfo = GetAppIdFromManifest(gameInstallPath);
        if (!basicInfo.HasValue)
        {
            return null;
        }

        var (appId, gameName, sizeOnDisk) = basicInfo.Value;

        // Find the manifest file again to get extended info
        string steamappsPath = GetSteamappsPath(gameInstallPath);
        if (string.IsNullOrEmpty(steamappsPath))
        {
            return null;
        }

        string gameFolderName = Path.GetFileName(gameInstallPath.TrimEnd('\\', '/'));
        var acfFiles = Directory.GetFiles(steamappsPath, "appmanifest_*.acf");

        foreach (var acfFile in acfFiles)
        {
            try
            {
                string content = File.ReadAllText(acfFile);
                var manifest = ParseAcfFile(content);

                if (manifest.TryGetValue("installdir", out var installDir) &&
                    string.Equals(installDir, gameFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    // Extract build ID and timestamp
                    string buildId = manifest.GetValueOrDefault("buildid", "0");
                    long lastUpdated = 0;
                    if (manifest.TryGetValue("LastUpdated", out var lastUpdatedStr))
                    {
                        long.TryParse(lastUpdatedStr, out lastUpdated);
                    }

                    // Parse installed depots
                    var depots = ParseInstalledDepots(content);

                    return (gameName, appId, sizeOnDisk, buildId, lastUpdated, depots);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MANIFEST] Error parsing full info from {acfFile}: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    ///     Parses the InstalledDepots section from ACF content
    /// </summary>
    private static Dictionary<string, (string manifest, long size)> ParseInstalledDepots(string acfContent)
    {
        var depots = new Dictionary<string, (string manifest, long size)>();

        try
        {
            // Find the InstalledDepots section
            var depotsSectionMatch = Regex.Match(acfContent, @"""InstalledDepots""\s*\{([\s\S]*?)\n\t\}",
                RegexOptions.Multiline);
            if (!depotsSectionMatch.Success)
            {
                return depots;
            }

            string depotsSection = depotsSectionMatch.Groups[1].Value;

            // Parse each depot entry: "depotId" { "manifest" "manifestId" "size" "sizeBytes" }
            var depotMatches = Regex.Matches(depotsSection,
                @"""(\d+)""\s*\{[^}]*""manifest""\s+""(\d+)""[^}]*""size""\s+""(\d+)""", RegexOptions.Multiline);

            foreach (Match match in depotMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string depotId = match.Groups[1].Value;
                    string manifestId = match.Groups[2].Value;
                    long size = 0;
                    long.TryParse(match.Groups[3].Value, out size);

                    depots[depotId] = (manifestId, size);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MANIFEST] Error parsing depots: {ex.Message}");
        }

        return depots;
    }

    /// <summary>
    ///     Detects if game is 64-bit or 32-bit based on executable files
    /// </summary>
    public static string DetectGamePlatform(string gamePath)
    {
        try
        {
            // Look for exe files
            var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories);
            foreach (var exeFile in exeFiles)
            {
                try
                {
                    // Read PE header to determine architecture
                    using var fs = new FileStream(exeFile, FileMode.Open, FileAccess.Read);
                    using var br = new BinaryReader(fs);

                    // Check for MZ header
                    if (br.ReadUInt16() != 0x5A4D)
                    {
                        continue; // "MZ"
                    }

                    fs.Seek(0x3C, SeekOrigin.Begin);
                    int peOffset = br.ReadInt32();
                    fs.Seek(peOffset, SeekOrigin.Begin);

                    // Check PE signature
                    if (br.ReadUInt32() != 0x00004550)
                    {
                        continue; // "PE\0\0"
                    }

                    // Read machine type
                    ushort machineType = br.ReadUInt16();
                    if (machineType == 0x8664)
                    {
                        return "Win64"; // AMD64
                    }

                    if (machineType == 0x014C)
                    {
                        return "Win32"; // i386
                    }
                }
                catch { }
            }
        }
        catch { }

        return "Win64"; // Default to Win64
    }
}
