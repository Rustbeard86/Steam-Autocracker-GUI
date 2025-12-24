using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of file system service that wraps standard .NET file operations.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.GetFiles(path, searchPattern, searchOption);

    public string[] GetDirectories(string path)
        => Directory.GetDirectories(path);

    public Task<string> ReadAllTextAsync(string path)
        => File.ReadAllTextAsync(path);

    public Task WriteAllTextAsync(string path, string contents)
        => File.WriteAllTextAsync(path, contents);

    public void DeleteFile(string path)
        => File.Delete(path);

    public void DeleteDirectory(string path, bool recursive)
        => Directory.Delete(path, recursive);

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false)
        => File.Copy(sourceFileName, destFileName, overwrite);

    public void MoveFile(string sourceFileName, string destFileName)
        => File.Move(sourceFileName, destFileName);

    public void CreateDirectory(string path)
        => Directory.CreateDirectory(path);

    public FileInfo GetFileInfo(string path) => new(path);

    public void CopyDirectory(string sourceDir, string targetDir)
    {
        // Ensure paths end with separator for proper replacement
        if (!sourceDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            sourceDir += Path.DirectorySeparatorChar;
        }

        if (!targetDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            targetDir += Path.DirectorySeparatorChar;
        }

        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Create all directories
        foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = dirPath.Substring(sourceDir.Length);
            Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
        }

        // Copy all files
        foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
        {
            string relativePath = filePath.Substring(sourceDir.Length);
            string targetPath = Path.Combine(targetDir, relativePath);
            File.Copy(filePath, targetPath, true);
        }
    }

    public int CountSteamApiDlls(string gamePath)
    {
        try
        {
            var count = Directory.GetFiles(gamePath, "steam_api*.dll", SearchOption.AllDirectories)
                .Count(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
                            (f.EndsWith("steam_api.dll", StringComparison.OrdinalIgnoreCase) ||
                             f.EndsWith("steam_api64.dll", StringComparison.OrdinalIgnoreCase)));
            return count;
        }
        catch { return 0; }
    }
}
