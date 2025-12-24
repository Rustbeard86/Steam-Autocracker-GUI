namespace APPID;

/// <summary>
///     Provides thread-safe logging functionality for the application.
///     Logs are written to %AppData%\SACGUI\debug.log with automatic rotation.
/// </summary>
public static class LogHelper
{
    private const long MaxLogSizeBytes = 10 * 1024 * 1024; // 10MB
    private static readonly Lock LockObject = new();
    private static bool _initialized;

    /// <summary>
    ///     Writes a message to the debug log file.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        try
        {
            lock (LockObject)
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SACGUI");

                Directory.CreateDirectory(appDataPath);
                string logFile = Path.Combine(appDataPath, "debug.log");

                // On first log of session, add separator and handle rotation
                if (!_initialized)
                {
                    _initialized = true;
                    RotateLogFileIfNeeded(logFile);
                    WriteSessionHeader(logFile);
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logFile, logEntry);
                Debug.WriteLine(logEntry.TrimEnd());
            }
        }
        catch (Exception ex)
        {
            // Fail silently but write to debug output
            Debug.WriteLine($"[LogHelper] Failed to write log: {ex.Message}");
        }
    }

    /// <summary>
    ///     Logs an error with exception details.
    /// </summary>
    /// <param name="context">The context in which the error occurred.</param>
    /// <param name="ex">The exception that was thrown.</param>
    public static void LogError(string context, Exception ex)
    {
        Log($"[ERROR] {context}: {ex.Message}");

        if (ex.StackTrace is not null)
        {
            Log($"[ERROR] Stack: {ex.StackTrace}");
        }

        if (ex.InnerException is not null)
        {
            Log($"[ERROR] Inner: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    ///     Logs a component update.
    /// </summary>
    public static void LogUpdate(string component, string version)
        => Log($"[UPDATE] {component} updated to version {version}");

    /// <summary>
    ///     Logs a network operation.
    /// </summary>
    public static void LogNetwork(string message)
        => Log($"[NETWORK] {message}");

    /// <summary>
    ///     Logs an API call result.
    /// </summary>
    public static void LogAPI(string api, string status)
        => Log($"[API] {api}: {status}");

    private static void RotateLogFileIfNeeded(string logFile)
    {
        if (!File.Exists(logFile))
        {
            return;
        }

        var fileInfo = new FileInfo(logFile);
        if (fileInfo.Length > MaxLogSizeBytes)
        {
            File.Delete(logFile);
        }
    }

    private static void WriteSessionHeader(string logFile)
    {
        string separator = $"""


                            =================================
                            SACGUI LAUNCHED
                            {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                            =================================

                            """;
        File.AppendAllText(logFile, separator);
    }
}
