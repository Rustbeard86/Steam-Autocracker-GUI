namespace APPID.Models;

/// <summary>
///     Represents a game item in batch processing
/// </summary>
public class BatchGameItem
{
    /// <summary>
    ///     Game name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Full path to game directory
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    ///     Steam AppID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    ///     Whether to crack this game
    /// </summary>
    public bool ShouldCrack { get; set; }

    /// <summary>
    ///     Whether to compress this game
    /// </summary>
    public bool ShouldZip { get; set; }

    /// <summary>
    ///     Whether to upload this game
    /// </summary>
    public bool ShouldUpload { get; set; }

    /// <summary>
    ///     Size of game directory in bytes (calculated)
    /// </summary>
    public long SizeBytes { get; set; }

    // Manifest info for clean files sharing (cs.rin.ru format)
    /// <summary>
    ///     Steam build ID from manifest
    /// </summary>
    public string BuildId { get; set; } = string.Empty;

    /// <summary>
    ///     Last updated Unix timestamp from manifest
    /// </summary>
    public long LastUpdated { get; set; }

    /// <summary>
    ///     Steam branch (e.g., "Public")
    /// </summary>
    public string Branch { get; set; } = "Public";

    /// <summary>
    ///     Platform (Win64, Win32, etc.)
    /// </summary>
    public string Platform { get; set; } = "Win64";

    /// <summary>
    ///     Installed depots with manifest IDs and sizes
    /// </summary>
    public Dictionary<string, (string manifest, long size)> InstalledDepots { get; set; } = new();
}
