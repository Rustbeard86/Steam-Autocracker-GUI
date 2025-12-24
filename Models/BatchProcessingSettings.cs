namespace APPID.Models;

/// <summary>
/// Settings for batch processing operations
/// </summary>
public record BatchProcessingSettings
{
    /// <summary>
    /// Compression format (7z or zip)
    /// </summary>
    public string CompressionFormat { get; init; } = "7z";

    /// <summary>
    /// Compression level (0-9)
    /// </summary>
    public string CompressionLevel { get; init; } = "5";

    /// <summary>
    /// Whether to use password protection (rin)
    /// </summary>
    public bool UsePassword { get; init; }

    /// <summary>
    /// Whether to use Goldberg emulator (vs ALI213)
    /// </summary>
    public bool UseGoldberg { get; init; } = true;

    /// <summary>
    /// Whether to convert 1fichier links to PyDrive
    /// </summary>
    public bool ConvertToPyDrive { get; init; } = true;

    /// <summary>
    /// Maximum number of concurrent uploads
    /// </summary>
    public int MaxConcurrentUploads { get; init; } = 3;

    /// <summary>
    /// Maximum retry attempts for failed operations
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Delay between retries in milliseconds
    /// </summary>
    public int RetryDelayMs { get; init; } = 2000;
}
