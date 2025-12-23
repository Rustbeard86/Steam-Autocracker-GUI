namespace APPID.Services.Interfaces;

/// <summary>
///     Service for batch game data operations like folder size calculation and formatting.
/// </summary>
public interface IBatchGameDataService
{
    /// <summary>
    ///     Gets the total size of a folder including all files and subfolders.
    /// </summary>
    /// <param name="folderPath">The folder path to calculate size for.</param>
    /// <returns>The total size in bytes.</returns>
    long GetFolderSize(string folderPath);

    /// <summary>
    ///     Formats a file size in bytes to a human-readable string (B, KB, MB, GB, TB).
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>A formatted string like "1.5 GB".</returns>
    string FormatFileSize(long bytes);

    /// <summary>
    ///     Gets the folder size as a formatted string.
    /// </summary>
    /// <param name="folderPath">The folder path.</param>
    /// <returns>A formatted size string, or "?" if size cannot be determined.</returns>
    string GetFolderSizeString(string folderPath);

    /// <summary>
    ///     Validates if a game folder contains required files and is suitable for processing.
    /// </summary>
    /// <param name="path">The game folder path.</param>
    /// <returns>True if the folder contains executable files and has non-zero size.</returns>
    bool ValidateGameFolder(string path);
}
