namespace APPID.Services.Interfaces;

/// <summary>
///     Service for detecting games in directories and validating game structures.
/// </summary>
public interface IGameDetectionService
{
    /// <summary>
    ///     Detects games in a given folder path by looking for steam_api DLLs and game-like structures.
    /// </summary>
    /// <param name="path">The directory path to search.</param>
    /// <returns>A list of detected game directory paths.</returns>
    List<string> DetectGamesInFolder(string path);

    /// <summary>
    ///     Checks if a folder looks like a game folder based on common indicators.
    /// </summary>
    /// <param name="folderPath">The folder path to check.</param>
    /// <returns>True if the folder appears to be a game folder, false otherwise.</returns>
    bool IsGameFolder(string folderPath);

    /// <summary>
    ///     Finds Steam API DLLs in a directory.
    /// </summary>
    /// <param name="path">The directory path to search.</param>
    /// <returns>A list of paths to steam_api.dll and steam_api64.dll files.</returns>
    List<string> FindSteamApiDlls(string path);
}
