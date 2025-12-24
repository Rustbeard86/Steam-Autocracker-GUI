using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of game detection service for Steam games.
/// </summary>
public sealed class GameDetectionService(IFileSystemService fileSystem) : IGameDetectionService
{
    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public List<string> DetectGamesInFolder(string path)
    {
        var games = new List<string>();
        if (!_fileSystem.DirectoryExists(path))
        {
            return games;
        }

        try
        {
            // First, search for all steam_api DLLs to find actual game folders
            // Ignore .bak files (those are backups from previous cracks)
            var steamApiFiles = new List<string>();
            try
            {
                steamApiFiles.AddRange(_fileSystem.GetFiles(path, "steam_api.dll", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)));
                steamApiFiles.AddRange(_fileSystem.GetFiles(path, "steam_api64.dll", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)));
            }
            catch { }

            // Get unique game folders from steam_api locations
            var gameFoldersFromDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dllPath in steamApiFiles)
            {
                // The game folder is typically the parent or grandparent of the DLL
                var dllFolder = Path.GetDirectoryName(dllPath);
                if (string.IsNullOrEmpty(dllFolder))
                {
                    continue;
                }

                // Check if this DLL folder is a direct child of the search path
                var relativePath = dllFolder.Substring(path.Length).TrimStart(Path.DirectorySeparatorChar);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                if (pathParts.Length == 0)
                {
                    continue;
                }

                var topLevelFolder = pathParts[0];
                var gameFolder = Path.Combine(path, topLevelFolder);

                if (_fileSystem.DirectoryExists(gameFolder))
                {
                    gameFoldersFromDlls.Add(gameFolder);
                }
            }

            // Add all folders that have steam_api DLLs
            games.AddRange(gameFoldersFromDlls);

            // Also check direct children that might be games without steam_api DLLs (fallback)
            var subfolders = _fileSystem.GetDirectories(path);
            foreach (var subfolder in subfolders)
            {
                if (!gameFoldersFromDlls.Contains(subfolder) && IsGameFolder(subfolder))
                {
                    // Only add if it looks like a game but wasn't found via steam_api
                    games.Add(subfolder);
                }
            }
        }
        catch { }

        return games.Distinct().ToList();
    }

    public bool IsGameFolder(string folderPath)
    {
        if (!_fileSystem.DirectoryExists(folderPath))
        {
            return false;
        }

        try
        {
            // Only consider it a crackable game if it has steam_api.dll or steam_api64.dll
            // This is the ONLY reliable indicator for Steam games that can be cracked
            var steamApiFolder = FindSteamApiFolder(folderPath, 2);
            return steamApiFolder != null;
        }
        catch
        {
            return false;
        }
    }

    public List<string> FindSteamApiDlls(string path)
    {
        var dllPaths = new List<string>();

        if (!_fileSystem.DirectoryExists(path))
        {
            return dllPaths;
        }

        try
        {
            dllPaths.AddRange(_fileSystem.GetFiles(path, "steam_api.dll", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)));
            dllPaths.AddRange(_fileSystem.GetFiles(path, "steam_api64.dll", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)));
        }
        catch { }

        return dllPaths;
    }

    private string? FindSteamApiFolder(string path, int maxDepth = 3)
    {
        if (!_fileSystem.DirectoryExists(path) || maxDepth < 0)
        {
            return null;
        }

        try
        {
            // Check current folder for steam_api DLLs
            if (_fileSystem.FileExists(Path.Combine(path, "steam_api.dll")) ||
                _fileSystem.FileExists(Path.Combine(path, "steam_api64.dll")))
            {
                return path;
            }

            // Check subfolders
            foreach (var subfolder in _fileSystem.GetDirectories(path))
            {
                // Skip common non-game folders
                string folderName = Path.GetFileName(subfolder);
                if (folderName.StartsWith("_CommonRedist", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("Redistributables", StringComparison.OrdinalIgnoreCase) ||
                    folderName.Equals("Redist", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var result = FindSteamApiFolder(subfolder, maxDepth - 1);
                if (result != null)
                {
                    return result;
                }
            }
        }
        catch { }

        return null;
    }
}
