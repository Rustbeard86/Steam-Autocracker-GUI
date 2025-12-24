namespace APPID.Models;

/// <summary>
/// Progress information for batch processing operations
/// </summary>
public record BatchProgress
{
    /// <summary>
    /// Current phase of batch processing (Cracking, Compressing, Uploading, Converting, Complete)
    /// </summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public int OverallPercentage { get; init; }

    /// <summary>
    /// Current phase progress percentage (0-100)
    /// </summary>
    public int PhasePercentage { get; init; }

    /// <summary>
    /// Estimated seconds remaining for entire batch
    /// </summary>
    public double EstimatedSecondsRemaining { get; init; }

    /// <summary>
    /// Current game being processed
    /// </summary>
    public string? GameName { get; init; }

    /// <summary>
    /// Current game index (1-based)
    /// </summary>
    public int CurrentGameIndex { get; init; }

    /// <summary>
    /// Total number of games
    /// </summary>
    public int TotalGames { get; init; }

    /// <summary>
    /// Status message for current operation
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Number of games successfully cracked
    /// </summary>
    public int CrackedCount { get; init; }

    /// <summary>
    /// Number of games successfully zipped
    /// </summary>
    public int ZippedCount { get; init; }

    /// <summary>
    /// Number of games successfully uploaded
    /// </summary>
    public int UploadedCount { get; init; }
}
