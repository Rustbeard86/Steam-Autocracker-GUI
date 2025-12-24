namespace APPID.Services.Interfaces;

/// <summary>
///     Service for converting 1fichier URLs to PyDrive high-speed download links.
/// </summary>
public interface IUrlConversionService
{
    /// <summary>
    ///     Converts a 1fichier URL to a PyDrive URL using the conversion API.
    /// </summary>
    /// <param name="oneFichierUrl">The 1fichier download URL.</param>
    /// <param name="statusCallback">Optional callback for status updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The converted PyDrive URL, or null if conversion fails.</returns>
    Task<string?> ConvertOneFichierToPyDriveAsync(string oneFichierUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Converts a 1fichier URL to a PyDrive URL with file size for better estimation
    /// </summary>
    /// <param name="oneFichierUrl">The 1fichier download URL</param>
    /// <param name="fileSizeBytes">File size in bytes for wait time calculation</param>
    /// <param name="statusCallback">Optional callback for status updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The converted PyDrive URL, or null if conversion fails</returns>
    Task<string?> ConvertOneFichierToPyDriveAsync(
        string oneFichierUrl,
        long fileSizeBytes,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a URL is a 1fichier URL.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if the URL is from 1fichier.com, false otherwise.</returns>
    bool IsOneFichierUrl(string url);

    /// <summary>
    ///     Checks if a URL is a PyDrive URL.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if the URL is from PyDrive, false otherwise.</returns>
    bool IsPyDriveUrl(string url);
}
