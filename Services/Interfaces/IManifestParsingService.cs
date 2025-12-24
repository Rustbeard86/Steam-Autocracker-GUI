namespace APPID.Services.Interfaces;

/// <summary>
///     Service for parsing Steam manifest files to detect and extract game information.
/// </summary>
public interface IManifestParsingService
{
    /// <summary>
    ///     Parses a Steam manifest file to extract app ID and installation directory.
    /// </summary>
    /// <param name="manifestPath">Path to the .acf manifest file.</param>
    /// <returns>A ManifestInfo object containing the parsed data, or null if parsing fails.</returns>
    ManifestInfo? ParseManifest(string manifestPath);

    /// <summary>
    ///     Finds all Steam manifest files in a directory.
    /// </summary>
    /// <param name="path">The directory path to search (typically steamapps folder).</param>
    /// <returns>A list of paths to .acf manifest files.</returns>
    List<string> FindManifestFiles(string path);

    /// <summary>
    ///     Detects the Steam library folders on the system.
    /// </summary>
    /// <returns>A list of Steam library folder paths.</returns>
    List<string> DetectSteamLibraryFolders();

    /// <summary>
    ///     Attempts to find AppID from Steam manifest files when a folder is dropped.
    /// </summary>
    /// <param name="droppedPath">The path that was dropped onto the application.</param>
    /// <returns>Tuple of (AppID, GameName, SizeOnDisk) or null if not found.</returns>
    (string appId, string gameName, long sizeOnDisk)? GetAppIdFromManifest(string droppedPath);

    /// <summary>
    ///     Gets full manifest info including build ID, depots, and timestamps.
    /// </summary>
    /// <param name="gameInstallPath">The game installation path.</param>
    /// <returns>Full manifest information or null if not found.</returns>
    (string gameName, string appId, long sizeOnDisk, string buildId, long lastUpdated,
        Dictionary<string, (string manifest, long size)> depots)? GetFullManifestInfo(string gameInstallPath);

    /// <summary>
    ///     Finds all installed Steam games by scanning manifest files.
    /// </summary>
    /// <returns>List of tuples containing (AppID, GameName, InstallPath) for each game.</returns>
    List<(string appId, string gameName, string installPath)> FindAllInstalledGames();

    /// <summary>
    ///     Gets AppID directly from a known manifest file path.
    /// </summary>
    /// <param name="acfFilePath">Path to the .acf manifest file.</param>
    /// <returns>The AppID, or null if not found.</returns>
    string? GetAppIdFromManifestFile(string acfFilePath);

    /// <summary>
    ///     Verifies if the actual folder size matches the manifest size (within tolerance).
    /// </summary>
    /// <param name="gamePath">The game installation path.</param>
    /// <param name="manifestSize">The size from the manifest.</param>
    /// <param name="toleranceBytes">Tolerance in bytes (default 10MB).</param>
    /// <returns>True if sizes match within tolerance.</returns>
    bool VerifyGameSize(string gamePath, long manifestSize, long toleranceBytes = 10485760);

    /// <summary>
    ///     Detects if game is 64-bit or 32-bit based on executable files.
    /// </summary>
    /// <param name="gamePath">The game installation path.</param>
    /// <returns>"Win64" or "Win32" based on detected architecture.</returns>
    string DetectGamePlatform(string gamePath);
}

/// <summary>
///     Represents parsed information from a Steam manifest file.
/// </summary>
public sealed record ManifestInfo
{
    /// <summary>The Steam App ID.</summary>
    public string AppId { get; init; } = string.Empty;

    /// <summary>The game's installation directory name.</summary>
    public string InstallDir { get; init; } = string.Empty;

    /// <summary>The game's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The full installation path (if resolved).</summary>
    public string? FullPath { get; init; }
}
