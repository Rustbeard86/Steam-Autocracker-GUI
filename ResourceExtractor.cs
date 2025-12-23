using System.Reflection;

namespace SteamAppIdIdentifier;

/// <summary>
/// Handles extraction of embedded _bin resources at runtime.
/// </summary>
public static class ResourceExtractor
{
    private static readonly string BinPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory,
        "_bin");

    private static readonly string[] KnownExtensions =
    [
        "exe", "dll", "png", "jpg", "bat", "ver",
        "old", "txt", "ini", "json", "config"
    ];

    /// <summary>
    /// Extracts embedded _bin resources to the file system if not already present.
    /// </summary>
    public static void ExtractBinFiles()
    {
        try
        {
            Assembly assembly = typeof(ResourceExtractor).Assembly;

            // Get all embedded resource names that start with _bin.
            string[] resourceNames = assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("_bin.", StringComparison.Ordinal))
                .ToArray();

            if (resourceNames.Length == 0)
            {
                // No embedded resources, assume _bin is already extracted
                return;
            }

            Directory.CreateDirectory(BinPath);

            foreach (string resourceName in resourceNames)
            {
                ExtractSingleResource(assembly, resourceName);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash - maybe _bin files are already there
            Debug.WriteLine($"Resource extraction error: {ex.Message}");
            LogHelper.LogError("Resource extraction", ex);
        }
    }

    /// <summary>
    /// Gets the full path to a file in the _bin directory.
    /// </summary>
    /// <param name="relativePath">Relative path within the _bin directory.</param>
    /// <returns>Full path to the file.</returns>
    public static string GetBinFilePath(string relativePath)
    {
        // Ensure resources are extracted first
        ExtractBinFiles();
        return Path.Combine(BinPath, relativePath);
    }

    private static void ExtractSingleResource(Assembly assembly, string resourceName)
    {
        // Convert resource name back to file path
        // Format: _bin.folder.subfolder.file.ext
        string relativePath = resourceName[5..]; // Remove "_bin."

        string fullPath = BuildFullPath(relativePath);

        // Skip if file already exists
        if (File.Exists(fullPath))
        {
            return;
        }

        // Create directory structure if needed
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        // Extract the resource
        using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream is null)
        {
            return;
        }

        using FileStream fileStream = File.Create(fullPath);
        resourceStream.CopyTo(fileStream);

        // Make exe files executable
        if (fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            File.SetAttributes(fullPath, FileAttributes.Normal);
        }
    }

    private static string BuildFullPath(string relativePath)
    {
        // Handle nested directories
        string[] parts = relativePath.Split('.');

        if (parts.Length < 2)
        {
            return Path.Combine(BinPath, relativePath);
        }

        // The last two parts are usually filename.extension
        string possibleExtension = parts[^1];
        string possibleName = parts[^2];

        // Check if it's a known file extension
        if (KnownExtensions.Contains(possibleExtension, StringComparer.OrdinalIgnoreCase))
        {
            string fileName = $"{possibleName}.{possibleExtension}";

            // Build directory path from remaining parts
            if (parts.Length > 2)
            {
                string dirPath = string.Join(Path.DirectorySeparatorChar, parts[..^2]);
                return Path.Combine(BinPath, dirPath, fileName);
            }

            return Path.Combine(BinPath, fileName);
        }

        // Assume it's all directory path
        string fullFileName = relativePath.Replace('.', Path.DirectorySeparatorChar);
        return Path.Combine(BinPath, fullFileName);
    }
}