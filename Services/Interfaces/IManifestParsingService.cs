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
