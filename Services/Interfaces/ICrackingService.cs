namespace APPID.Services.Interfaces;

/// <summary>
/// Service for handling Steam game cracking operations.
/// </summary>
public interface ICrackingService
{
    /// <summary>
    /// Performs the core cracking operation on a game directory.
    /// </summary>
    /// <param name="gameDir">The game directory path.</param>
    /// <param name="appId">The Steam App ID.</param>
    /// <param name="useGoldberg">True to use Goldberg emulator, false for ALI213.</param>
    /// <param name="statusCallback">Callback for status updates.</param>
    /// <returns>A task that returns true if successful, false otherwise.</returns>
    Task<CrackResult> CrackGameAsync(string gameDir, string appId, bool useGoldberg, Action<string>? statusCallback = null);

    /// <summary>
    /// Checks if two files are identical.
    /// </summary>
    bool AreFilesIdentical(string file1, string file2);
}

/// <summary>
/// Result of a cracking operation.
/// </summary>
public sealed record CrackResult
{
    public bool Success { get; init; }
    public List<string> DllsReplaced { get; init; } = [];
    public List<string> DllsBackedUp { get; init; } = [];
    public List<string> ExesUnpacked { get; init; } = [];
    public List<string> ExesTried { get; init; } = [];
    public string? ErrorMessage { get; init; }
}
