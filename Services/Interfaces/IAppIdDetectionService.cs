namespace APPID.Services.Interfaces;

/// <summary>
///     Service for detecting Steam AppIDs from game folders and search queries.
/// </summary>
public interface IAppIdDetectionService
{
    /// <summary>
    ///     Detects the Steam AppID for a game from its installation folder.
    ///     Tries multiple detection methods: manifest files, steam_appid.txt, API search, and local database.
    /// </summary>
    /// <param name="gamePath">The game installation folder path.</param>
    /// <returns>The detected AppID, or null if not found.</returns>
    string? DetectAppId(string gamePath);

    /// <summary>
    ///     Searches the Steam Store API for a game by name.
    ///     Has excellent fuzzy matching and filters out non-game content.
    /// </summary>
    /// <param name="gameName">The game name to search for.</param>
    /// <returns>The AppID of the best match, or null if not found.</returns>
    string? SearchSteamStoreApi(string gameName);

    /// <summary>
    ///     Searches the local Steam database for a game by folder name.
    ///     Provides fallback when API search fails or is unavailable.
    /// </summary>
    /// <param name="folderName">The game folder name to search for.</param>
    /// <returns>The AppID if found in local database, or null if not found.</returns>
    string? SearchSteamDbByFolderName(string folderName);
}
