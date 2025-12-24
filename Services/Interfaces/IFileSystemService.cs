namespace APPID.Services.Interfaces;

/// <summary>
///     Service for wrapping file system operations to improve testability.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    ///     Checks if a file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    ///     Checks if a directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    ///     Gets all files in a directory with the specified search pattern.
    /// </summary>
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    ///     Gets all subdirectories in a directory.
    /// </summary>
    string[] GetDirectories(string path);

    /// <summary>
    ///     Reads all text from a file.
    /// </summary>
    Task<string> ReadAllTextAsync(string path);

    /// <summary>
    ///     Writes all text to a file.
    /// </summary>
    Task WriteAllTextAsync(string path, string contents);

    /// <summary>
    ///     Deletes a file.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    ///     Deletes a directory recursively.
    /// </summary>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>
    ///     Copies a file.
    /// </summary>
    void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);

    /// <summary>
    ///     Moves a file.
    /// </summary>
    void MoveFile(string sourceFileName, string destFileName);

    /// <summary>
    ///     Creates a directory.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    ///     Gets file information.
    /// </summary>
    FileInfo GetFileInfo(string path);

    /// <summary>
    ///     Recursively copies a directory and all its contents.
    /// </summary>
    /// <param name="sourceDir">The source directory to copy from.</param>
    /// <param name="targetDir">The target directory to copy to.</param>
    void CopyDirectory(string sourceDir, string targetDir);

    /// <summary>
    ///     Counts the number of steam_api*.dll files in a directory.
    /// </summary>
    /// <param name="gamePath">The path to search for Steam API DLLs.</param>
    /// <returns>The count of steam_api.dll or steam_api64.dll files found (excluding .bak files).</returns>
    int CountSteamApiDlls(string gamePath);
}
