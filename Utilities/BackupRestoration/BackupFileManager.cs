namespace APPID.Utilities.BackupRestoration;

/// <summary>
///     Manages restoration of backup files (.bak) created during the cracking process.
///     This utility will be used for future features to restore original game files.
/// </summary>
public static class BackupFileManager
{
    /// <summary>
    ///     Restores all .bak files in the specified directory and its subdirectories.
    ///     Original cracked files are deleted and .bak files are renamed to their original names.
    /// </summary>
    /// <param name="directory">The root directory to search for .bak files</param>
    /// <returns>A RestoreResult containing information about the restoration process</returns>
    public static RestoreResult RestoreAllBackupFiles(string directory)
    {
        var result = new RestoreResult();

        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                result.Errors.Add("Directory path cannot be empty");
                result.Success = false;
                return result;
            }

            if (!Directory.Exists(directory))
            {
                result.Errors.Add($"Directory does not exist: {directory}");
                result.Success = false;
                return result;
            }

            var bakFiles = Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories);
            result.TotalBackupsFound = bakFiles.Length;

            if (bakFiles.Length == 0)
            {
                Debug.WriteLine($"[BACKUP_RESTORE] No .bak files found in: {directory}");
                result.Success = true;
                result.Summary = "No backup files to restore";
                return result;
            }

            Debug.WriteLine($"[BACKUP_RESTORE] Found {bakFiles.Length} backup files to restore in: {directory}");

            foreach (var bakFile in bakFiles)
            {
                try
                {
                    string originalFile = bakFile[..^4]; // Remove .bak extension
                    string fileName = Path.GetFileName(originalFile);

                    // Delete the modified/cracked file if it exists
                    if (File.Exists(originalFile))
                    {
                        File.Delete(originalFile);
                        Debug.WriteLine($"[BACKUP_RESTORE] Deleted modified file: {fileName}");
                    }

                    // Restore the backup by renaming it
                    File.Move(bakFile, originalFile);
                    result.RestoredFiles.Add(fileName);

                    Debug.WriteLine($"[BACKUP_RESTORE] Restored {Path.GetFileName(bakFile)} -> {fileName}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    string error = $"Permission denied for {Path.GetFileName(bakFile)}: {ex.Message}";
                    result.Errors.Add(error);
                    result.FailedFiles.Add(Path.GetFileName(bakFile));
                    Debug.WriteLine($"[BACKUP_RESTORE] {error}");
                }
                catch (IOException ex)
                {
                    string error = $"IO error for {Path.GetFileName(bakFile)}: {ex.Message}";
                    result.Errors.Add(error);
                    result.FailedFiles.Add(Path.GetFileName(bakFile));
                    Debug.WriteLine($"[BACKUP_RESTORE] {error}");
                }
                catch (Exception ex)
                {
                    string error = $"Failed to restore {Path.GetFileName(bakFile)}: {ex.Message}";
                    result.Errors.Add(error);
                    result.FailedFiles.Add(Path.GetFileName(bakFile));
                    Debug.WriteLine($"[BACKUP_RESTORE] {error}");
                }
            }

            // Calculate success
            result.Success = result.Errors.Count == 0;
            result.Summary = result.Success
                ? $"Successfully restored {result.RestoredFiles.Count} files"
                : $"Restored {result.RestoredFiles.Count} files with {result.Errors.Count} errors";

            Debug.WriteLine($"[BACKUP_RESTORE] Restoration complete: {result.Summary}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Critical error during restoration: {ex.Message}");
            result.Summary = "Restoration failed";
            Debug.WriteLine($"[BACKUP_RESTORE] Critical error: {ex.Message}");
            Debug.WriteLine($"[BACKUP_RESTORE] Stack trace: {ex.StackTrace}");
        }

        return result;
    }

    /// <summary>
    ///     Restores backup files matching a specific pattern (e.g., "*.dll.bak", "*.exe.bak").
    /// </summary>
    /// <param name="directory">The root directory to search</param>
    /// <param name="searchPattern">The search pattern for backup files (e.g., "*.dll.bak")</param>
    /// <returns>A RestoreResult containing information about the restoration process</returns>
    public static RestoreResult RestoreBackupFilesByPattern(string directory, string searchPattern)
    {
        var result = new RestoreResult();

        try
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                result.Errors.Add("Directory path cannot be empty");
                result.Success = false;
                return result;
            }

            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                result.Errors.Add("Search pattern cannot be empty");
                result.Success = false;
                return result;
            }

            if (!Directory.Exists(directory))
            {
                result.Errors.Add($"Directory does not exist: {directory}");
                result.Success = false;
                return result;
            }

            var bakFiles = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
            result.TotalBackupsFound = bakFiles.Length;

            Debug.WriteLine(
                $"[BACKUP_RESTORE] Found {bakFiles.Length} files matching pattern '{searchPattern}' in: {directory}");

            foreach (var bakFile in bakFiles)
            {
                try
                {
                    // Extract original filename based on pattern
                    string originalFile;

                    if (searchPattern.EndsWith(".bak"))
                    {
                        // Standard .bak pattern - remove last 4 characters
                        originalFile = bakFile[..^4];
                    }
                    else
                    {
                        // Custom pattern - try to determine original name
                        // This might need adjustment based on actual patterns used
                        string bakFileName = Path.GetFileName(bakFile);
                        if (bakFileName.EndsWith(".bak"))
                        {
                            originalFile = Path.Combine(Path.GetDirectoryName(bakFile)!,
                                bakFileName[..^4]);
                        }
                        else
                        {
                            result.Errors.Add($"Cannot determine original name for: {bakFileName}");
                            result.FailedFiles.Add(bakFileName);
                            continue;
                        }
                    }

                    string fileName = Path.GetFileName(originalFile);

                    // Delete the modified file if it exists
                    if (File.Exists(originalFile))
                    {
                        File.Delete(originalFile);
                    }

                    // Restore the backup
                    File.Move(bakFile, originalFile);
                    result.RestoredFiles.Add(fileName);

                    Debug.WriteLine($"[BACKUP_RESTORE] Restored {Path.GetFileName(bakFile)} -> {fileName}");
                }
                catch (Exception ex)
                {
                    string error = $"Failed to restore {Path.GetFileName(bakFile)}: {ex.Message}";
                    result.Errors.Add(error);
                    result.FailedFiles.Add(Path.GetFileName(bakFile));
                    Debug.WriteLine($"[BACKUP_RESTORE] {error}");
                }
            }

            result.Success = result.Errors.Count == 0;
            result.Summary = result.Success
                ? $"Successfully restored {result.RestoredFiles.Count} files matching '{searchPattern}'"
                : $"Restored {result.RestoredFiles.Count} files with {result.Errors.Count} errors";

            Debug.WriteLine($"[BACKUP_RESTORE] Pattern restoration complete: {result.Summary}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Critical error during pattern restoration: {ex.Message}");
            result.Summary = "Restoration failed";
            Debug.WriteLine($"[BACKUP_RESTORE] Critical error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    ///     Checks if backup files exist in the specified directory.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <returns>True if any .bak files are found</returns>
    public static bool HasBackupFiles(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            return Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Gets a count of backup files in the specified directory.
    /// </summary>
    /// <param name="directory">The directory to check</param>
    /// <returns>The number of .bak files found</returns>
    public static int GetBackupFileCount(string directory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     Lists all backup files in the specified directory with their original filenames.
    /// </summary>
    /// <param name="directory">The directory to search</param>
    /// <returns>A list of BackupFileInfo objects</returns>
    public static List<BackupFileInfo> ListBackupFiles(string directory)
    {
        var backups = new List<BackupFileInfo>();

        try
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return backups;
            }

            var bakFiles = Directory.GetFiles(directory, "*.bak", SearchOption.AllDirectories);

            foreach (var bakFile in bakFiles)
            {
                try
                {
                    string originalName = Path.GetFileNameWithoutExtension(bakFile);
                    var fileInfo = new FileInfo(bakFile);

                    backups.Add(new BackupFileInfo
                    {
                        BackupPath = bakFile,
                        OriginalPath = bakFile[..^4],
                        OriginalName = originalName,
                        BackupName = Path.GetFileName(bakFile),
                        Size = fileInfo.Length,
                        CreatedDate = fileInfo.CreationTime,
                        ModifiedDate = fileInfo.LastWriteTime
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BACKUP_RESTORE] Error reading backup info: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BACKUP_RESTORE] Error listing backups: {ex.Message}");
        }

        return backups;
    }
}

/// <summary>
///     Contains the result of a backup restoration operation.
/// </summary>
public class RestoreResult
{
    /// <summary>
    ///     Whether the restoration was successful (no errors).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    ///     Total number of backup files found.
    /// </summary>
    public int TotalBackupsFound { get; set; }

    /// <summary>
    ///     List of successfully restored filenames.
    /// </summary>
    public List<string> RestoredFiles { get; set; } = [];

    /// <summary>
    ///     List of filenames that failed to restore.
    /// </summary>
    public List<string> FailedFiles { get; set; } = [];

    /// <summary>
    ///     List of error messages encountered during restoration.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    ///     A human-readable summary of the restoration operation.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    ///     Gets a detailed summary including counts and errors.
    /// </summary>
    public string GetDetailedSummary()
    {
        var summary = "Restoration Summary:\n";
        summary += $"  Total backups found: {TotalBackupsFound}\n";
        summary += $"  Successfully restored: {RestoredFiles.Count}\n";
        summary += $"  Failed: {FailedFiles.Count}\n";

        if (Errors.Count > 0)
        {
            summary += "\nErrors:\n";
            foreach (var error in Errors)
            {
                summary += $"  • {error}\n";
            }
        }

        return summary;
    }
}

/// <summary>
///     Information about a backup file.
/// </summary>
public class BackupFileInfo
{
    /// <summary>
    ///     Full path to the backup file (.bak).
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    ///     Full path to the original file (without .bak extension).
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    ///     Original filename (without .bak extension).
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    ///     Backup filename (with .bak extension).
    /// </summary>
    public string BackupName { get; set; } = string.Empty;

    /// <summary>
    ///     Size of the backup file in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    ///     When the backup file was created.
    /// </summary>
    public DateTime CreatedDate { get; set; }

    /// <summary>
    ///     When the backup file was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    /// <summary>
    ///     Human-readable file size.
    /// </summary>
    public string FormattedSize
    {
        get
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            double len = Size;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
