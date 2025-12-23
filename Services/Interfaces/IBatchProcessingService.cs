namespace APPID.Services.Interfaces;

/// <summary>
///     Progress update information for batch operations.
/// </summary>
public sealed record BatchProgress
{
    /// <summary>Current operation phase (e.g., "Cracking", "Zipping", "Uploading").</summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>Name of the game being processed.</summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>Current game index (1-based).</summary>
    public int CurrentGameIndex { get; init; }

    /// <summary>Total number of games.</summary>
    public int TotalGames { get; init; }

    /// <summary>Percentage complete (0-100).</summary>
    public int PercentComplete { get; init; }

    /// <summary>Status message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Whether the current operation succeeded.</summary>
    public bool Success { get; init; } = true;
}

/// <summary>
///     Result of a batch game processing operation.
/// </summary>
public sealed record BatchProcessingResult
{
    /// <summary>Number of games successfully cracked.</summary>
    public int CrackedCount { get; init; }

    /// <summary>Number of games successfully zipped.</summary>
    public int ZippedCount { get; init; }

    /// <summary>Number of games successfully uploaded.</summary>
    public int UploadedCount { get; init; }

    /// <summary>Number of games that failed to crack.</summary>
    public int CrackFailedCount { get; init; }

    /// <summary>Number of games that failed to zip.</summary>
    public int ZipFailedCount { get; init; }

    /// <summary>Number of games that failed to upload.</summary>
    public int UploadFailedCount { get; init; }

    /// <summary>Upload results with URLs.</summary>
    public List<UploadResultInfo> UploadResults { get; init; } = [];
}

/// <summary>
///     Information about an uploaded game.
/// </summary>
public sealed record UploadResultInfo
{
    /// <summary>Game name.</summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>1fichier download URL.</summary>
    public string OneFichierUrl { get; init; } = string.Empty;

    /// <summary>PyDrive download URL (if converted).</summary>
    public string? PyDriveUrl { get; init; }
}

/// <summary>
///     Settings for batch processing operations.
/// </summary>
public sealed record BatchProcessingSettings
{
    /// <summary>Whether to crack games.</summary>
    public bool DoCrack { get; init; } = true;

    /// <summary>Whether to zip games.</summary>
    public bool DoZip { get; init; }

    /// <summary>Whether to upload games.</summary>
    public bool DoUpload { get; init; }

    /// <summary>Compression format (zip or 7z).</summary>
    public string CompressionFormat { get; init; } = "zip";

    /// <summary>Compression level.</summary>
    public string CompressionLevel { get; init; } = "Normal";

    /// <summary>Whether to use password protection.</summary>
    public bool UsePassword { get; init; }

    /// <summary>Whether to use Goldberg emulator.</summary>
    public bool UseGoldberg { get; init; } = true;
}

/// <summary>
///     Represents a game to be processed in a batch operation.
/// </summary>
public sealed record BatchGameItem
{
    /// <summary>Game name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Game installation path.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Steam App ID.</summary>
    public string AppId { get; init; } = string.Empty;

    /// <summary>Whether this game should be cracked.</summary>
    public bool ShouldCrack { get; init; } = true;

    /// <summary>Whether this game should be zipped.</summary>
    public bool ShouldZip { get; init; }

    /// <summary>Whether this game should be uploaded.</summary>
    public bool ShouldUpload { get; init; }
}

/// <summary>
///     Service for coordinating batch cracking, zipping, and uploading workflows.
/// </summary>
public interface IBatchProcessingService
{
    /// <summary>
    ///     Processes multiple games with cracking, compression, and upload operations.
    /// </summary>
    /// <param name="games">List of games to process.</param>
    /// <param name="settings">Batch processing settings.</param>
    /// <param name="progress">Progress reporter for UI updates.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that returns the batch processing result.</returns>
    Task<BatchProcessingResult> ProcessBatchGamesAsync(
        List<BatchGameItem> games,
        BatchProcessingSettings settings,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
