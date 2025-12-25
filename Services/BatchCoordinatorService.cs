using APPID.Dialogs;
using APPID.Models;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Coordinates batch game processing operations including cracking, compression, and upload.
///     Handles UI updates, progress tracking, and result formatting.
/// </summary>
public sealed class BatchCoordinatorService : IBatchCoordinatorService
{
    private readonly IBatchProcessingService _batchProcessingService;

    public BatchCoordinatorService(IBatchProcessingService batchProcessingService)
    {
        _batchProcessingService = batchProcessingService ??
                                  throw new ArgumentNullException(nameof(batchProcessingService));
    }

    /// <summary>
    ///     Processes a batch of games with the specified settings and updates UI accordingly.
    /// </summary>
    /// <param name="games">List of games to process</param>
    /// <param name="compressionFormat">Compression format (7z or zip)</param>
    /// <param name="compressionLevel">Compression level</param>
    /// <param name="usePassword">Whether to use password encryption</param>
    /// <param name="useGoldberg">Whether to use Goldberg emulator</param>
    /// <param name="convertToPyDrive">Whether to convert 1fichier URLs to PyDrive</param>
    /// <param name="batchForm">The batch form for UI updates</param>
    /// <param name="statusUpdateCallback">Callback for status messages</param>
    /// <param name="batchIndicatorUpdateCallback">Callback for batch indicator updates</param>
    public async Task ProcessBatchGamesAsync(
        List<BatchGameItem> games,
        string compressionFormat,
        string compressionLevel,
        bool usePassword,
        bool useGoldberg,
        bool convertToPyDrive,
        BatchGameSelectionForm batchForm,
        Action<string, Color>? statusUpdateCallback = null,
        Action<int>? batchIndicatorUpdateCallback = null)
    {
        var settings = new BatchProcessingSettings
        {
            CompressionFormat = compressionFormat,
            CompressionLevel = compressionLevel,
            UsePassword = usePassword,
            UseGoldberg = useGoldberg,
            ConvertToPyDrive = convertToPyDrive,
            MaxConcurrentUploads = 3,
            MaxRetries = 3,
            RetryDelayMs = 2000
        };

        var progress = new Progress<BatchProgress>(p =>
        {
            // Update batch form
            batchForm.UpdateTitleProgress(p.OverallPercentage);
            batchForm.UpdateProgressWithEta(p.OverallPercentage, p.EstimatedSecondsRemaining);

            // Update batch indicator
            batchIndicatorUpdateCallback?.Invoke(p.OverallPercentage);

            // Update status with color coding
            if (!string.IsNullOrEmpty(p.GameName))
            {
                Color color = p.Phase switch
                {
                    "Cracking" => Color.Yellow,
                    "Compressing" => Color.Cyan,
                    "Uploading" => Color.Magenta,
                    "Converting" => Color.Orange,
                    _ => Color.White
                };
                batchForm.UpdateStatus(p.GameName, p.Message, color);
            }
        });

        batchForm.SetProcessingMode(true);

        var result = await _batchProcessingService.ProcessBatchGamesAsync(games, settings, progress);

        batchForm.SetProcessingMode(false);
        batchForm.ResetTitle("Complete ✓");
        batchIndicatorUpdateCallback?.Invoke(100);

        // Show summary
        statusUpdateCallback?.Invoke($"Batch complete! {result.GetSummary()}", Color.LightGreen);

        // Show copy all button with upload URLs (enhanced cs.rin.ru format with manifest info)
        if (result.UploadResults.Count > 0)
        {
            string formattedLinks = FormatUploadResultsForForum(games, result.UploadResults);
            batchForm.ShowCopyAllButton(formattedLinks);
        }

        // Show failures if any
        if (result.HasFailures && result.Failures.Count > 0)
        {
            ShowFailureDialog(result.Failures);
        }
    }

    /// <summary>
    ///     Formats upload results in phpBB format for cs.rin.ru with manifest information.
    /// </summary>
    private static string FormatUploadResultsForForum(
        List<BatchGameItem> games,
        List<UploadResultInfo> uploadResults)
    {
        var linksWithManifestInfo = new List<string>();

        foreach (var upload in uploadResults)
        {
            // Find the corresponding BatchGameItem to get manifest info
            var gameItem = games.FirstOrDefault(g => g.Name == upload.GameName);

            if (gameItem != null && !string.IsNullOrEmpty(gameItem.BuildId))
            {
                // Format version date from Unix timestamp
                string versionDate = "Unknown";
                if (gameItem.LastUpdated > 0)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(gameItem.LastUpdated).UtcDateTime;
                    versionDate = $"{dt:MMM dd, yyyy - HH:mm:ss} UTC [Build {gameItem.BuildId}]";
                }

                // Build depot list
                var depotLines = new List<string>();
                foreach (var depot in gameItem.InstalledDepots)
                {
                    depotLines.Add($"{depot.Key} [Manifest {depot.Value.manifest}]");
                }

                string depotsText = depotLines.Count > 0
                    ? string.Join("\n", depotLines)
                    : "No depot info";

                // Full phpBB format for cs.rin.ru
                string formattedLink =
                    $"[url={upload.FinalUrl}][color=white][b]{gameItem.Name} [{gameItem.Platform}] [Branch: {gameItem.Branch}] (Clean Steam Files)[/b][/color][/url]\n" +
                    $"[size=85][color=white][b]Version:[/b] [i]{versionDate}[/i][/color][/size]\n\n" +
                    $"[spoiler=\"[color=white]Depots & Manifests[/color]\"][code=text]{depotsText}[/code][/spoiler]" +
                    $"[color=white][b]Uploaded version:[/b] [i]{versionDate}[/i][/color]";

                linksWithManifestInfo.Add(formattedLink);
            }
            else
            {
                // Fallback to simple format if no manifest info
                linksWithManifestInfo.Add($"[url={upload.FinalUrl}]{upload.GameName}[/url]");
            }
        }

        return string.Join("\n\n", linksWithManifestInfo);
    }

    /// <summary>
    ///     Shows a dialog with batch processing failures.
    /// </summary>
    private static void ShowFailureDialog(List<(string gameName, string reason)> failures)
    {
        var failureMessages = string.Join("\n",
            failures.Take(10).Select(f => $"- {f.gameName}: {f.reason}"));

        if (failures.Count > 10)
        {
            failureMessages += $"\n... and {failures.Count - 10} more";
        }

        MessageBox.Show(
            $"Some operations failed:\n\n{failureMessages}",
            "Batch Processing Failures",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
