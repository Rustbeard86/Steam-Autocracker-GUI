namespace APPID.Services.Interfaces;

/// <summary>
///     Service for DLC (Downloadable Content) operations.
/// </summary>
public interface IDlcService
{
    /// <summary>
    ///     Fetches DLC information from the Steam Store API and saves it to a file.
    /// </summary>
    /// <param name="appId">The Steam App ID to fetch DLC information for.</param>
    /// <param name="outputFolder">The folder where the DLC.txt file will be saved.</param>
    /// <param name="statusCallback">Optional callback for status updates (message, color).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FetchDlcInfoAsync(string appId, string outputFolder, Action<string, Color>? statusCallback = null);
}
