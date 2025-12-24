namespace APPID.Utilities.Steam;

/// <summary>
///     Helper class for detecting and working with Steam folder structures.
///     Consolidates all folder detection logic from GameFolderService, SteamManifestParser, etc.
/// </summary>
public static class SteamFolderStructureHelper
{
    // Common game engine subfolder patterns
    private static readonly string[] UnitySubfolders = ["_Data", "Mono", "MonoBleedingEdge"];
    private static readonly string[] UnrealSubfolders = ["Binaries", "Content", "Engine", "Plugins"];
    private static readonly string[] SourceEngineSubfolders = ["bin", "cfg", "maps", "materials", "models"];
    private static readonly string[] GenericSubfolders = ["data", "assets", "resources"];

    /// <summary>
    ///     Determines if a folder is likely a game subfolder rather than the root.
    /// </summary>
    /// <param name="folderName">The folder name to check</param>
    /// <returns>True if this appears to be a subfolder</returns>
    public static bool IsGameSubfolder(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
        {
            return false;
        }

        // Check Unity patterns (GameName_Data)
        if (UnitySubfolders.Any(pattern =>
                folderName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check Unreal/Source Engine patterns
        if (UnrealSubfolders.Concat(SourceEngineSubfolders).Concat(GenericSubfolders)
            .Any(pattern => folderName.Equals(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Detects the game engine type from folder structure.
    /// </summary>
    /// <param name="gamePath">The game installation path</param>
    /// <returns>The detected engine type</returns>
    public static GameEngineType DetectGameEngine(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            return GameEngineType.Unknown;
        }

        try
        {
            var directories = Directory.GetDirectories(gamePath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Unity detection
            if (UnitySubfolders.Any(folder => directories.Contains(folder)))
            {
                return GameEngineType.Unity;
            }

            // Unreal Engine detection
            if (UnrealSubfolders.Any(folder => directories.Contains(folder)))
            {
                return GameEngineType.UnrealEngine;
            }

            // Source Engine detection
            if (SourceEngineSubfolders.Count(folder => directories.Contains(folder)) >= 2)
            {
                return GameEngineType.SourceEngine;
            }

            // Check for specific engine DLLs
            var files = Directory.GetFiles(gamePath, "*.dll", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (files.Contains("UnityPlayer"))
            {
                return GameEngineType.Unity;
            }

            if (files.Any(f => f.Contains("UE4") || f.Contains("UE5")))
            {
                return GameEngineType.UnrealEngine;
            }

            return GameEngineType.Unknown;
        }
        catch
        {
            return GameEngineType.Unknown;
        }
    }

    /// <summary>
    ///     Finds all Steam library folders on the system by parsing libraryfolders.vdf.
    /// </summary>
    /// <returns>List of Steam library paths</returns>
    public static List<string> FindAllLibraries()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Default Steam installation paths
        var defaultPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam")
        };

        // Add existing default paths
        foreach (var path in defaultPaths.Where(Directory.Exists))
        {
            paths.Add(path);
        }

        // Parse libraryfolders.vdf for additional libraries
        foreach (var steamPath in paths.ToList())
        {
            var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                var additionalPaths = ParseLibraryFolders(vdfPath);
                foreach (var additionalPath in additionalPaths)
                {
                    paths.Add(additionalPath);
                }
            }
        }

        return paths.ToList();
    }

    /// <summary>
    ///     Parses libraryfolders.vdf to extract Steam library paths.
    /// </summary>
    /// <param name="vdfPath">Path to libraryfolders.vdf</param>
    /// <returns>List of library paths</returns>
    public static List<string> ParseLibraryFolders(string vdfPath)
    {
        var paths = new List<string>();

        if (!File.Exists(vdfPath))
        {
            return paths;
        }

        try
        {
            string content = File.ReadAllText(vdfPath);

            // Look for path entries: "path" "C:\\SteamLibrary"
            var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""path""\s+""([^""]*)""");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    string libPath = match.Groups[1].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(libPath))
                    {
                        paths.Add(libPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[STEAM FOLDERS] Error parsing {vdfPath}: {ex.Message}");
        }

        return paths;
    }

    /// <summary>
    ///     Finds the steamapps directory from a given game path by walking up the tree.
    /// </summary>
    /// <param name="gamePath">A path within a Steam game installation</param>
    /// <returns>The steamapps directory path, or null if not found</returns>
    public static string? FindSteamappsDirectory(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            return null;
        }

        var dir = new DirectoryInfo(gamePath);

        while (dir != null)
        {
            // Check if this is the steamapps folder
            if (dir.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                return dir.FullName;
            }

            // Check if this is common folder (parent is steamapps)
            if (dir.Name.Equals("common", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
            {
                return dir.Parent.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    ///     Determines if a path is within a Steam library structure.
    /// </summary>
    /// <param name="path">The path to check</param>
    /// <returns>True if the path is within a Steam library</returns>
    public static bool IsInSteamLibrary(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Check if path contains "steamapps" anywhere in the hierarchy
        return path.Contains("steamapps", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Gets the install directory name from a full game path.
    ///     Handles cases where the game is in steamapps/common/GameName.
    /// </summary>
    /// <param name="gamePath">The full game installation path</param>
    /// <returns>The install directory name (the folder name in steamapps/common)</returns>
    public static string GetInstallDirectoryName(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath))
        {
            return string.Empty;
        }

        // If path contains steamapps/common, get the folder after common
        var parts = gamePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("common", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }

        // Otherwise just return the last folder name
        return Path.GetFileName(gamePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}

/// <summary>
///     Enum representing detected game engine types.
/// </summary>
public enum GameEngineType
{
    Unknown,
    Unity,
    UnrealEngine,
    SourceEngine,
    Custom
}
