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

    public FileInfo GetFileInfo(string path)
        => new FileInfo(path);
}
