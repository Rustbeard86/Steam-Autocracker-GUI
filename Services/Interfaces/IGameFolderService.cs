namespace APPID.Services.Interfaces;

/// <summary>
///     Service for game folder operations including name extraction and validation.
/// </summary>
public interface IGameFolderService
{
    /// <summary>
    ///     Extracts the game name from a folder path, automatically detecting and correcting common issues
    ///     like Unity _Data folders or other game subdirectories.
    /// </summary>
    /// <param name="folderPath">The full path to the game folder.</param>
    /// <returns>The game name extracted from the folder path.</returns>
    string GetGameName(string folderPath);

    /// <summary>
    ///     Finds the actual game root folder from a given path, walking up the directory tree if needed
    ///     to detect and correct selections of subdirectories like Game_Data folders.
    /// </summary>
    /// <param name="selectedPath">The path that was selected by the user.</param>
    /// <returns>The corrected game root folder path.</returns>
    string FindGameRootFolder(string selectedPath);

    /// <summary>
    ///     Checks if a folder name indicates it's a subdirectory rather than a game root.
    /// </summary>
    /// <param name="folderName">The folder name to check.</param>
    /// <returns>True if the folder is likely a subdirectory, false otherwise.</returns>
    bool IsSubfolder(string folderName);

    /// <summary>
    ///     Gets the parent folder of a given path if one exists.
    /// </summary>
    /// <param name="folderPath">The folder path.</param>
    /// <returns>The parent folder path, or null if no parent exists.</returns>
    string? GetParentFolder(string folderPath);
}