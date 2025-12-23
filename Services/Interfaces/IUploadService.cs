namespace APPID.Services.Interfaces;

/// <summary>
///     Service for handling file upload operations.
/// </summary>
public interface IUploadService
{
    /// <summary>
    ///     Uploads a file to the backend sharing service.
    /// </summary>
    /// <param name="filePath">Path to the file to upload.</param>
    /// <param name="gameName">Name of the game being uploaded.</param>
    /// <param name="progressCallback">Optional callback for upload progress (0-100).</param>
    /// <returns>A task that returns the upload URL if successful, or null if failed.</returns>
    Task<string?> UploadFileAsync(string filePath, string gameName, Action<int>? progressCallback = null);
}
