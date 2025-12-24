namespace APPID.Models;

/// <summary>
/// Result of batch processing operation
/// </summary>
public record BatchProcessingResult
{
    /// <summary>
    /// Number of games successfully cracked
    /// </summary>
    public int CrackedCount { get; init; }

    /// <summary>
    /// Number of games that failed to crack
    /// </summary>
    public int CrackFailedCount { get; init; }

    /// <summary>
    /// Number of games successfully compressed
    /// </summary>
    public int ZippedCount { get; init; }

    /// <summary>
    /// Number of games that failed to compress
    /// </summary>
    public int ZipFailedCount { get; init; }

    /// <summary>
    /// Number of games successfully uploaded
    /// </summary>
    public int UploadedCount { get; init; }

    /// <summary>
    /// Number of games that failed to upload
    /// </summary>
    public int UploadFailedCount { get; init; }

    /// <summary>
    /// Upload results with URLs
    /// </summary>
    public List<UploadResultInfo> UploadResults { get; init; } = new();

    /// <summary>
    /// Crack details for each game
    /// </summary>
    public Dictionary<string, CrackDetails> CrackDetails { get; init; } = new();

    /// <summary>
    /// Failure reasons for debugging
    /// </summary>
    public List<(string gameName, string reason)> Failures { get; init; } = new();

    /// <summary>
    /// Total processing time
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Whether any operation succeeded
    /// </summary>
    public bool HasSuccess => CrackedCount > 0 || ZippedCount > 0 || UploadedCount > 0;

    /// <summary>
    /// Whether any operation failed
    /// </summary>
    public bool HasFailures => CrackFailedCount > 0 || ZipFailedCount > 0 || UploadFailedCount > 0;

    /// <summary>
    /// Summary message for display
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();
        if (CrackedCount > 0) parts.Add($"{CrackedCount} cracked");
        if (ZippedCount > 0) parts.Add($"{ZippedCount} zipped");
        if (UploadedCount > 0) parts.Add($"{UploadedCount} uploaded");
        if (CrackFailedCount > 0) parts.Add($"{CrackFailedCount} crack failed");
        if (ZipFailedCount > 0) parts.Add($"{ZipFailedCount} zip failed");
        if (UploadFailedCount > 0) parts.Add($"{UploadFailedCount} upload failed");
        
        return parts.Count > 0 ? string.Join(", ", parts) : "No operations performed";
    }
}

/// <summary>
/// Information about an uploaded file
/// </summary>
public record UploadResultInfo
{
    /// <summary>
    /// Game name
    /// </summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>
    /// Original 1fichier URL
    /// </summary>
    public string OneFichierUrl { get; init; } = string.Empty;

    /// <summary>
    /// Converted PyDrive URL (if conversion succeeded)
    /// </summary>
    public string? PyDriveUrl { get; init; }

    /// <summary>
    /// Final URL to use (PyDrive if available, otherwise 1fichier)
    /// </summary>
    public string FinalUrl => PyDriveUrl ?? OneFichierUrl;
}
