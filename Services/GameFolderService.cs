using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Service for game folder operations including name extraction and validation.
/// </summary>
public sealed class GameFolderService(IFileSystemService fileSystem, IGameDetectionService gameDetection)
    : IGameFolderService
{
    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IGameDetectionService _gameDetection = gameDetection ?? throw new ArgumentNullException(nameof(gameDetection));

    // Common subfolder patterns that indicate we're in a subdirectory, not the game root
    private static readonly string[] SubfolderPatterns =
    [
        "_Data",           // Unity: Game_Data
        "_Binaries",       // UE: GameName_Binaries
        "Binaries",        // UE binaries folder
        "Content",         // UE content folder
        "Engine",          // UE engine folder  
        "bin",             // Common bin folder
        "data"             // Common data folder
    ];

    public string GetGameName(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return string.Empty;
        }

        // First find the actual game root (in case user selected a subfolder)
        string gameRoot = FindGameRootFolder(folderPath);

        // Extract just the folder name from the full path
        return Path.GetFileName(gameRoot);
    }

    public string FindGameRootFolder(string selectedPath)
    {
        if (string.IsNullOrEmpty(selectedPath) || !_fileSystem.DirectoryExists(selectedPath))
        {
            return selectedPath;
        }

        string folderName = Path.GetFileName(selectedPath);

        // Check if current folder name matches a subfolder pattern
        if (IsSubfolder(folderName))
        {
            var parent = Directory.GetParent(selectedPath);
            if (parent != null && _gameDetection.IsGameFolder(parent.FullName))
            {
                // Parent is the real game folder
                Debug.WriteLine($"[GAME FOLDER] Detected subfolder '{folderName}', moving up to '{parent.Name}'");
                return parent.FullName;
            }
        }

        // Check if parent has EXE files but current doesn't (common indicator)
        var parent2 = Directory.GetParent(selectedPath);
        if (parent2 != null)
        {
            bool currentHasExe = HasExecutableFiles(selectedPath);
            bool parentHasExe = HasExecutableFiles(parent2.FullName);

            if (parentHasExe && !currentHasExe && _gameDetection.IsGameFolder(parent2.FullName))
            {
                Debug.WriteLine($"[GAME FOLDER] Parent has EXE files, current doesn't - using parent: {parent2.Name}");
                return parent2.FullName;
            }
        }

        // Already at game root
        return selectedPath;
    }

    public bool IsSubfolder(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            return false;
        }

        // Case-insensitive check against known patterns
        return SubfolderPatterns.Any(pattern =>
            folderName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals(pattern, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetParentFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return null;
        }

        var parent = Directory.GetParent(folderPath);
        return parent?.FullName;
    }

    private bool HasExecutableFiles(string path)
    {
        try
        {
            return _fileSystem.GetFiles(path, "*.exe", SearchOption.TopDirectoryOnly).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
