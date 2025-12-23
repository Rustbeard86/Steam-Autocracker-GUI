using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of batch game data service for folder operations and formatting.
/// </summary>
public sealed class BatchGameDataService : IBatchGameDataService
{
    private readonly IFileSystemService _fileSystem;

    public BatchGameDataService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public long GetFolderSize(string folderPath)
    {
        long size = 0;
        try
        {
            foreach (var file in _fileSystem.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch { }
            }
        }
        catch { }

        return size;
    }

    public string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return $"{size:0.#} {suffixes[i]}";
    }

    public string GetFolderSizeString(string folderPath)
    {
        try
        {
            long size = GetFolderSize(folderPath);
            return FormatFileSize(size);
        }
        catch
        {
            return "?";
        }
    }

    public bool ValidateGameFolder(string path)
    {
        if (!_fileSystem.DirectoryExists(path))
        {
            return false;
        }

        try
        {
            // Check if folder has executable files
            bool hasExe = _fileSystem.GetFiles(path, "*.exe", SearchOption.AllDirectories).Any();
            if (!hasExe)
            {
                return false;
            }

            // Check if folder has non-zero size using the existing GetFolderSize method
            long size = GetFolderSize(path);
            if (size == 0)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
