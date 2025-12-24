using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of batch processing service for coordinating game cracking, compression, and upload workflows.
///     Note: This is a simplified initial extraction. Full implementation would require additional refactoring
///     to remove UI dependencies (forms, progress callbacks, etc.)
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
        var result = new BatchProcessingResult();
        var uploadResults = new List<UploadResultInfo>();

        try
        {
            // Phase 0: Clean up old crack artifacts
            await CleanupCrackArtifactsAsync(games, cancellationToken).ConfigureAwait(false);

            // Phase 1: Crack games
            int currentIndex = 0;
            foreach (var game in games.Where(g => g.ShouldCrack))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                currentIndex++;
                progress?.Report(new BatchProgress
                {
                    Phase = "Cracking",
                    GameName = game.Name,
                    CurrentGameIndex = currentIndex,
                    TotalGames = games.Count,
                    Message = $"Cracking {game.Name}..."
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
                        result = result with { CrackedCount = result.CrackedCount + 1 };
                        LogHelper.Log($"[BATCH] Successfully cracked: {game.Name}");
                    }
                    else
                    {
                        result = result with { CrackFailedCount = result.CrackFailedCount + 1 };
                        LogHelper.Log($"[BATCH] Failed to crack: {game.Name} - {crackResult.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    result = result with { CrackFailedCount = result.CrackFailedCount + 1 };
                    LogHelper.LogError($"[BATCH] Exception cracking {game.Name}", ex);
                }
            }

            // Phase 2: Compress games
            currentIndex = 0;
            foreach (var game in games.Where(g => g.ShouldZip))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                currentIndex++;
                progress?.Report(new BatchProgress
                {
                    Phase = "Compressing",
                    GameName = game.Name,
                    CurrentGameIndex = currentIndex,
                    TotalGames = games.Count(g => g.ShouldZip),
                    Message = $"Compressing {game.Name}..."
                });

                try
                {
                    string parentDir = Path.GetDirectoryName(game.Path)!;
                    string extension = settings.CompressionFormat.Equals("7z", StringComparison.OrdinalIgnoreCase)
                        ? ".7z"
                        : ".zip";
                    string outputPath = Path.Combine(parentDir, game.Name + extension);

                    bool compressResult = await _compressionService.CompressAsync(
                        game.Path,
                        outputPath,
                        settings.CompressionFormat,
                        settings.CompressionLevel,
                        settings.UsePassword,
                        percent => progress?.Report(new BatchProgress
                        {
                            Phase = "Compressing",
                            GameName = game.Name,
                            PercentComplete = percent,
                            Message = $"Compressing {game.Name}... {percent}%"
                        })).ConfigureAwait(false);

                    if (compressResult)
                    {
                        result = result with { ZippedCount = result.ZippedCount + 1 };
                        LogHelper.Log($"[BATCH] Successfully compressed: {game.Name}");
                    }
                    else
                    {
                        result = result with { ZipFailedCount = result.ZipFailedCount + 1 };
                        LogHelper.Log($"[BATCH] Failed to compress: {game.Name}");
                    }
                }
                catch (Exception ex)
                {
                    result = result with { ZipFailedCount = result.ZipFailedCount + 1 };
                    LogHelper.LogError($"[BATCH] Exception compressing {game.Name}", ex);
                }
            }

            // Phase 3: Upload games (placeholder - full upload logic requires form integration)
            // This would need to be implemented with proper upload service integration
            currentIndex = 0;
            foreach (var game in games.Where(g => g.ShouldUpload))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                currentIndex++;
                progress?.Report(new BatchProgress
                {
                    Phase = "Uploading",
                    GameName = game.Name,
                    CurrentGameIndex = currentIndex,
                    TotalGames = games.Count(g => g.ShouldUpload),
                    Message = $"Uploading {game.Name}..."
                });

                // Note: Upload implementation would go here
                // For now, we increment the uploaded count as placeholder
                LogHelper.Log($"[BATCH] Upload placeholder for: {game.Name}");
            }

            result = result with { UploadResults = uploadResults };

            return result;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("[BATCH] Fatal error in batch processing", ex);
            return result;
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
            var dirs = _fileSystem.GetFiles(basePath, searchPattern, SearchOption.AllDirectories);
            foreach (var dir in dirs)
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
}
