namespace APPID.Services.Interfaces;

/// <summary>
/// Service for handling Steamless EXE unpacking operations.
/// </summary>
public interface ISteamlessService
{
    /// <summary>
    /// Attempts to unpack a Steam-protected EXE using Steamless.
    /// </summary>
    /// <param name="exePath">Path to the EXE file to unpack.</param>
    /// <param name="workingDirectory">Working directory for the operation.</param>
    /// <param name="statusCallback">Optional callback for status updates.</param>
    /// <returns>A task that returns the result of the unpacking operation.</returns>
    Task<SteamlessResult> UnpackExeAsync(string exePath, string workingDirectory, Action<string>? statusCallback = null);
}

/// <summary>
/// Result of a Steamless unpacking operation.
/// </summary>
public sealed class SteamlessResult
{
    public bool Success { get; init; }
    public bool UnpackedFileCreated { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
}
