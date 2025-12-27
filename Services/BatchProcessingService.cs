using APPID.Models;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of batch processing service for coordinating game cracking, compression, and upload workflows.
///     Supports concurrent uploads, retry logic, progress tracking with time estimation, and URL conversion.
/// </summary>
public sealed class BatchProcessingService(
    ICrackingService crackingService,
    ICompressionService compressionService,
    IUploadService uploadService,
    IUrlConversionService urlConversionService,
    IFileSystemService fileSystem)
    : IBatchProcessingService
{
    private readonly ICompressionService _compressionService =
        compressionService ?? throw new ArgumentNullException(nameof(compressionService));

    private readonly ICrackingService _crackingService =
        crackingService ?? throw new ArgumentNullException(nameof(crackingService));

    private readonly IFileSystemService _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    private readonly IUploadService _uploadService =
        uploadService ?? throw new ArgumentNullException(nameof(uploadService));

    private readonly IUrlConversionService _urlConversionService =
        urlConversionService ?? throw new ArgumentNullException(nameof(urlConversionService));

    public async Task<BatchProcessingResult> ProcessBatchGamesAsync(
        List<BatchGameItem> games,
        BatchProcessingSettings settings,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new BatchProcessingResult();
        var uploadResults = new List<UploadResultInfo>();
        var failures = new List<(string gameName, string reason)>();

        // Initialize progress tracker
        var progressTracker = new BatchProgressTracker(games, settings);

        try
        {
            // Phase 0: Clean up old crack artifacts
            await CleanupCrackArtifactsAsync(games, cancellationToken).ConfigureAwait(false);

            // Phase 1: Crack games (sequential due to shared state)
            int crackedCount = 0;
            int crackFailedCount = 0;
            foreach (var game in games.Where(g => g.ShouldCrack))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                progress?.Report(new BatchProgress
                {
                    Phase = "Cracking", GameName = game.Name, Message = $"Cracking {game.Name}..."
                });

                try
                {
                    var crackResult = await _crackingService.CrackGameAsync(
                        game.Path,
                        game.AppId,
                        settings.UseGoldberg,
                        status => progress?.Report(new BatchProgress
                        {
                            Phase = "Cracking", GameName = game.Name, Message = status
                        })).ConfigureAwait(false);

                    if (crackResult.Success)
                    {
                        crackedCount++;
                        LogHelper.Log($"[BATCH] Successfully cracked: {game.Name}");
                    }
                    else
                    {
                        crackFailedCount++;
                        failures.Add((game.Name, crackResult.ErrorMessage ?? "Unknown crack error"));
                        LogHelper.Log($"[BATCH] Failed to crack: {game.Name} - {crackResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    crackFailedCount++;
                    failures.Add((game.Name, $"Crack exception: {ex.Message}"));
                    LogHelper.LogError($"[BATCH] Exception cracking {game.Name}", ex);
                }

                // Update progress after each crack
                var progressUpdate = progressTracker.UpdateForCrackComplete();
                progress?.Report(progressUpdate);
            }

            // Phase 2 & 3: Compression + Upload Pipeline
            // Pipeline: compress sequentially, upload concurrently (max 3 at once)
            var gamesToZip = games.Where(g => g.ShouldZip).ToList();
            var uploadSemaphore = new SemaphoreSlim(settings.MaxConcurrentUploads, settings.MaxConcurrentUploads);
            var uploadTasks = new List<Task<(bool success, UploadResultInfo? info, string? error)>>();
            var archivePaths = new Dictionary<string, string>();

            int zippedCount = 0;
            int zipFailedCount = 0;

            // Compress each game sequentially
            foreach (var game in gamesToZip)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                progress?.Report(new BatchProgress
                {
                    Phase = "Compressing", GameName = game.Name, Message = $"Compressing {game.Name}..."
                });

                try
                {
                    string parentDir = Path.GetDirectoryName(game.Path)!;
                    string extension = settings.CompressionFormat.Equals("7z", StringComparison.OrdinalIgnoreCase)
                        ? ".7z"
                        : ".zip";
                    
                    // Format: [SACGUI] GameName - Clean/Cracked (Build 12345).7z
                    string crackStatus = game.ShouldCrack ? "Cracked" : "Clean";
                    string buildSuffix = !string.IsNullOrEmpty(game.BuildId) ? $" (Build {game.BuildId})" : "";
                    string outputPath = Path.Combine(parentDir, $"[SACGUI] {game.Name} - {crackStatus}{buildSuffix}{extension}");
                    
                    archivePaths[game.Path] = outputPath;

                    bool compressResult = await _compressionService.CompressAsync(
                        game.Path,
                        outputPath,
                        settings.CompressionFormat,
                        settings.CompressionLevel,
                        settings.UsePassword,
                        percent =>
                        {
                            var progressUpdate = progressTracker.UpdateForZipProgress(game.Path, percent);
                            progress?.Report(progressUpdate with
                            {
                                GameName = game.Name, Message = $"Compressing {game.Name}... {percent}%"
                            });
                        }).ConfigureAwait(false);

                    if (compressResult)
                    {
                        zippedCount++;
                        LogHelper.Log($"[BATCH] Successfully compressed: {game.Name}");

                        // Fire off upload immediately if requested
                        if (game.ShouldUpload)
                        {
                            var uploadTask = UploadGameAsync(
                                game,
                                outputPath,
                                settings,
                                uploadSemaphore,
                                progressTracker,
                                progress,
                                cancellationToken);
                            uploadTasks.Add(uploadTask);
                        }
                    }
                    else
                    {
                        zipFailedCount++;
                        failures.Add((game.Name, "Compression failed"));
                        LogHelper.Log($"[BATCH] Failed to compress: {game.Name}");
                    }
                }
                catch (Exception ex)
                {
                    zipFailedCount++;
                    failures.Add((game.Name, $"Compression exception: {ex.Message}"));
                    LogHelper.LogError($"[BATCH] Exception compressing {game.Name}", ex);
                }
            }

            // Wait for all uploads to complete
            if (uploadTasks.Count > 0)
            {
                var uploadTaskResults = await Task.WhenAll(uploadTasks).ConfigureAwait(false);

                int uploadedCount = 0;
                int uploadFailedCount = 0;

                foreach (var (success, info, error) in uploadTaskResults)
                {
                    if (success && info != null)
                    {
                        uploadedCount++;
                        uploadResults.Add(info);
                    }
                    else
                    {
                        uploadFailedCount++;
                        if (!string.IsNullOrEmpty(error))
                        {
                            failures.Add(("Unknown game", error));
                        }
                    }
                }

                result = result with { UploadedCount = uploadedCount, UploadFailedCount = uploadFailedCount };
            }

            // Final progress update
            progress?.Report(progressTracker.GetFinalProgress());

            // Build final result
            result = result with
            {
                CrackedCount = crackedCount,
                CrackFailedCount = crackFailedCount,
                ZippedCount = zippedCount,
                ZipFailedCount = zipFailedCount,
                UploadResults = uploadResults,
                Failures = failures,
                ProcessingTime = DateTime.Now - startTime
            };

            return result;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("[BATCH] Fatal error in batch processing", ex);
            failures.Add(("Batch", $"Fatal error: {ex.Message}"));
            return result with { Failures = failures, ProcessingTime = DateTime.Now - startTime };
        }
    }

    /// <summary>
    ///     Uploads a single game with retry logic and URL conversion
    /// </summary>
    private async Task<(bool success, UploadResultInfo? info, string? error)> UploadGameAsync(
        BatchGameItem game,
        string archivePath,
        BatchProcessingSettings settings,
        SemaphoreSlim uploadSemaphore,
        BatchProgressTracker progressTracker,
        IProgress<BatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        await uploadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!_fileSystem.FileExists(archivePath))
            {
                return (false, null, $"{game.Name}: Archive not found");
            }

            var fileInfo = new FileInfo(archivePath);
            long fileSize = fileInfo.Length;
            string? oneFichierUrl = null;
            string? pyDriveUrl = null;
            string? lastError = null;

            // Retry logic for upload
            for (int attempt = 1; attempt <= settings.MaxRetries; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return (false, null, $"{game.Name}: Cancelled");
                }

                try
                {
                    progress?.Report(new BatchProgress
                    {
                        Phase = "Uploading",
                        GameName = game.Name,
                        Message = attempt > 1
                            ? $"Retry {attempt}: Uploading {game.Name}..."
                            : $"Uploading {game.Name}..."
                    });

                    // Upload to 1fichier
                    var uploadProgress = new Progress<(long bytesTransferred, long totalBytes, double speed)>(p =>
                    {
                        var progressUpdate =
                            progressTracker.UpdateForUploadProgress(game.Path, p.bytesTransferred, p.totalBytes,
                                p.speed);
                        progress?.Report(progressUpdate with
                        {
                            GameName = game.Name,
                            Message = $"⬆ {p.bytesTransferred * 100 / p.totalBytes}% | {p.speed / 1_000_000:F1}MB/s"
                        });
                    });

                    oneFichierUrl = await _uploadService.UploadToOneFichierAsync(
                        archivePath,
                        uploadProgress,
                        cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(oneFichierUrl))
                    {
                        LogHelper.Log($"[BATCH] Uploaded to 1fichier: {game.Name}");

                        // Convert to PyDrive if enabled
                        if (settings.ConvertToPyDrive)
                        {
                            progress?.Report(new BatchProgress
                            {
                                Phase = "Converting",
                                GameName = game.Name,
                                Message = $"Converting {game.Name} to PyDrive..."
                            });

                            var conversionProgress = progressTracker.UpdateForConversionProgress(game.Path);
                            progress?.Report(conversionProgress with { GameName = game.Name });

                            pyDriveUrl = await _urlConversionService.ConvertOneFichierToPyDriveAsync(
                                oneFichierUrl,
                                fileSize,
                                status => progress?.Report(new BatchProgress
                                {
                                    Phase = "Converting", GameName = game.Name, Message = status
                                }),
                                cancellationToken).ConfigureAwait(false);

                            if (!string.IsNullOrEmpty(pyDriveUrl))
                            {
                                LogHelper.Log($"[BATCH] Converted to PyDrive: {game.Name}");
                            }
                        }

                        return (true,
                            new UploadResultInfo
                            {
                                GameName = game.Name, OneFichierUrl = oneFichierUrl, PyDriveUrl = pyDriveUrl
                            }, null);
                    }

                    lastError = "No URL returned from upload";
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    LogHelper.LogError($"[BATCH] Upload attempt {attempt} failed for {game.Name}", ex);

                    if (attempt < settings.MaxRetries)
                    {
                        await Task.Delay(settings.RetryDelayMs * attempt, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return (false, null, $"{game.Name}: Upload failed after {settings.MaxRetries} attempts - {lastError}");
        }
        finally
        {
            uploadSemaphore.Release();
        }
    }

    private async Task CleanupCrackArtifactsAsync(List<BatchGameItem> games, CancellationToken cancellationToken)
    {
        foreach (var game in games)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                string installPath = game.Path;
                if (string.IsNullOrEmpty(installPath) || !_fileSystem.DirectoryExists(installPath))
                {
                    continue;
                }

                await Task.Run(() =>
                {
                    // Restore .bak files (original Steam DLLs and EXEs)
                    RestoreBackupFiles(installPath, "*.dll.bak");
                    RestoreBackupFiles(installPath, "*.exe.bak");

                    // Delete steam_settings directories
                    DeleteMatchingDirectories(installPath, "steam_settings");

                    // Delete crack-related files
                    DeleteMatchingFiles(installPath, "_[*");
                    DeleteMatchingFiles(installPath, "_lobby_connect*");
                    DeleteMatchingFiles(installPath, "lobby_connect*");
                    DeleteMatchingFiles(installPath, "*.lnk");

                    // Delete common crack artifacts
                    string[] artifacts =
                    [
                        "CreamAPI.dll", "cream_api.ini", "CreamLinux",
                        "steam_api_o.dll", "steam_api64_o.dll", "local_save.txt"
                    ];

                    foreach (var artifact in artifacts)
                    {
                        DeleteMatchingFiles(installPath, artifact);
                    }
                }, cancellationToken).ConfigureAwait(false);

                LogHelper.Log($"[BATCH] Cleaned up artifacts for: {game.Name}");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[BATCH] Failed to cleanup artifacts for {game.Name}", ex);
            }
        }
    }

    private void RestoreBackupFiles(string basePath, string searchPattern)
    {
        try
        {
            var bakFiles = _fileSystem.GetFiles(basePath, searchPattern, SearchOption.AllDirectories);
            foreach (var bakFile in bakFiles)
            {
                try
                {
                    var originalFile = bakFile.Substring(0, bakFile.Length - 4); // Remove ".bak"
                    if (_fileSystem.FileExists(originalFile))
                    {
                        _fileSystem.DeleteFile(originalFile);
                    }

                    _fileSystem.MoveFile(bakFile, originalFile);
                }
                catch { }
            }
        }
        catch { }
    }

    private void DeleteMatchingFiles(string basePath, string searchPattern)
    {
        try
        {
            var files = _fileSystem.GetFiles(basePath, searchPattern, SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    _fileSystem.DeleteFile(file);
                }
                catch { }
            }
        }
        catch { }
    }

    private void DeleteMatchingDirectories(string basePath, string searchPattern)
    {
        try
        {
            // Get all directories first
            var allDirs = _fileSystem.GetDirectories(basePath);

            // Find matching directories recursively
            var matchingDirs = new List<string>();
            foreach (var dir in allDirs)
            {
                if (Path.GetFileName(dir).Equals(searchPattern, StringComparison.OrdinalIgnoreCase))
                {
                    matchingDirs.Add(dir);
                }

                // Check subdirectories recursively
                try
                {
                    matchingDirs.AddRange(FindMatchingDirectoriesRecursive(dir, searchPattern));
                }
                catch { }
            }

            // Delete all matching directories
            foreach (var dir in matchingDirs)
            {
                try
                {
                    _fileSystem.DeleteDirectory(dir, true);
                }
                catch { }
            }
        }
        catch { }
    }

    private List<string> FindMatchingDirectoriesRecursive(string basePath, string searchPattern)
    {
        var results = new List<string>();
        try
        {
            var subdirs = _fileSystem.GetDirectories(basePath);
            foreach (var subdir in subdirs)
            {
                if (Path.GetFileName(subdir).Equals(searchPattern, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(subdir);
                }

                // Recurse into subdirectory
                try
                {
                    results.AddRange(FindMatchingDirectoriesRecursive(subdir, searchPattern));
                }
                catch { }
            }
        }
        catch { }

        return results;
    }
}
