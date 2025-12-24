namespace APPID.Models;

/// <summary>
/// Represents a game item in batch processing
/// </summary>
public class BatchGameItem
{
    /// <summary>
    /// Game name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to game directory
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Steam AppID
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Whether to crack this game
    /// </summary>
    public bool ShouldCrack { get; set; }

    /// <summary>
    /// Whether to compress this game
    /// </summary>
    public bool ShouldZip { get; set; }

    /// <summary>
    /// Whether to upload this game
    /// </summary>
    public bool ShouldUpload { get; set; }

    /// <summary>
    /// Size of game directory in bytes (calculated)
    /// </summary>
    public long SizeBytes { get; set; }
}
