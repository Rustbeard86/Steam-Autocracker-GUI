using APPID.Services.Interfaces;
using APPID.Utilities.Steam;

namespace APPID.Services;

/// <summary>
///     Implementation of manifest parsing service for Steam ACF files.
///     Uses centralized AcfFileParser for parsing logic.
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

            // Use centralized ACF parser
            var manifest = AcfFileParser.ParseFlat(content);

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
        // Use centralized folder structure helper
        return SteamFolderStructureHelper.FindAllLibraries();
    }

    public (string appId, string gameName, long sizeOnDisk)? GetAppIdFromManifest(string droppedPath)
    {
        // Check if path is in Steam library
        if (!SteamFolderStructureHelper.IsInSteamLibrary(droppedPath))
        {
            Debug.WriteLine("[MANIFEST] Path doesn't contain 'steamapps', skipping manifest check");
            return null;
        }

        // Get install directory name
        string gameFolderName = SteamFolderStructureHelper.GetInstallDirectoryName(droppedPath);
        Debug.WriteLine($"[MANIFEST] Checking for game folder: {gameFolderName}");

        // Find the steamapps directory
        string? steamappsPath = SteamFolderStructureHelper.FindSteamappsDirectory(droppedPath);
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
                var manifest = AcfFileParser.ParseFlat(content);

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

    public (string gameName, string appId, long sizeOnDisk, string buildId, long lastUpdated,
        Dictionary<string, (string manifest, long size)> depots)? GetFullManifestInfo(string gameInstallPath)
    {
        var basicInfo = GetAppIdFromManifest(gameInstallPath);
        if (!basicInfo.HasValue)
        {
            return null;
        }

        var (appId, gameName, sizeOnDisk) = basicInfo.Value;

        // Find the manifest file again to get extended info
        string? steamappsPath = SteamFolderStructureHelper.FindSteamappsDirectory(gameInstallPath);
        if (string.IsNullOrEmpty(steamappsPath))
        {
            return null;
        }

        string gameFolderName = SteamFolderStructureHelper.GetInstallDirectoryName(gameInstallPath);
        var acfFiles = Directory.GetFiles(steamappsPath, "appmanifest_*.acf");

        foreach (var acfFile in acfFiles)
        {
            try
            {
                string content = File.ReadAllText(acfFile);
                var manifest = AcfFileParser.ParseFlat(content);

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

                    // Parse installed depots using centralized parser
                    var depots = AcfFileParser.ParseInstalledDepots(content);

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

    public List<(string appId, string gameName, string installPath)> FindAllInstalledGames()
    {
        var games = new List<(string, string, string)>();
        var steamPaths = SteamFolderStructureHelper.FindAllLibraries();

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
                    var manifest = AcfFileParser.ParseFlat(content);

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

    public string? GetAppIdFromManifestFile(string acfFilePath)
    {
        try
        {
            string content = File.ReadAllText(acfFilePath);
            var manifest = AcfFileParser.ParseFlat(content);
            return manifest.GetValueOrDefault("appid");
        }
        catch
        {
            return null;
        }
    }

    public bool VerifyGameSize(string gamePath, long manifestSize, long toleranceBytes = 10485760)
    {
        long actualSize = GetDirectorySize(gamePath);
        long difference = Math.Abs(actualSize - manifestSize);

        Debug.WriteLine("[MANIFEST] Size verification:");
        Debug.WriteLine($"[MANIFEST] Manifest size: {manifestSize / (1024 * 1024)} MB");
        Debug.WriteLine($"[MANIFEST] Actual size: {actualSize / (1024 * 1024)} MB");
        Debug.WriteLine($"[MANIFEST] Difference: {difference / (1024 * 1024)} MB");

        return difference <= toleranceBytes;
    }

    public string DetectGamePlatform(string gamePath)
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
}
