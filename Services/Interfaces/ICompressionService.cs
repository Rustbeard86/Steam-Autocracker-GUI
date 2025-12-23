namespace APPID.Services.Interfaces;

/// <summary>
///     Service for handling file compression operations.
/// </summary>
public interface ICompressionService
{
    /// <summary>
    ///     Compresses a directory to an archive file.
    /// </summary>
    /// <param name="sourcePath">Source directory path.</param>
    /// <param name="outputPath">Output archive path.</param>
    /// <param name="format">Archive format (zip or 7z).</param>
    /// <param name="level">Compression level (No Compression, Fast, Normal, Maximum, Ultra).</param>
    /// <param name="usePassword">Whether to encrypt with cs.rin.ru password.</param>
    /// <param name="progressCallback">Optional callback for progress updates (0-100).</param>
    /// <returns>A task that returns true if successful, false otherwise.</returns>
    Task<bool> CompressAsync(string sourcePath, string outputPath, string format, string level, bool usePassword,
        Action<int>? progressCallback = null);
}
