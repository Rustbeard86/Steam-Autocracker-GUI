using APPID.Models;

namespace APPID.Services.Interfaces;

/// <summary>
/// Service for coordinating batch processing operations
/// </summary>
public interface IBatchProcessingService
{
    /// <summary>
    /// Process multiple games with crack, compression, and upload
    /// </summary>
    Task<BatchProcessingResult> ProcessBatchGamesAsync(
        List<BatchGameItem> games,
        BatchProcessingSettings settings,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
