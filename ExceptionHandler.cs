namespace APPID;

internal static class ExceptionHandler
{
    public static void ConfigureGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.ExceptionObject as Exception);
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
    }

    private static void WriteCrashLog(Exception? ex)
    {
        try
        {
            string crashInfo = BuildCrashReport(ex);
            string crashFile = Path.Combine(AppContext.BaseDirectory, "crash.log");

            File.WriteAllText(crashFile, crashInfo);
            LogToDebugFile(ex);
            ShowCrashDialog(crashFile, ex);
        }
        catch
        {
            ShowFallbackErrorDialog();
        }
    }

    private static string BuildCrashReport(Exception? ex)
    {
        return $"""
                =================================
                SACGUI CRASH REPORT
                {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                =================================
                Version: {Application.ProductVersion}
                OS: {Environment.OSVersion}
                .NET: {Environment.Version}

                Exception:
                {ex?.ToString() ?? "Unknown exception"}
                """;
    }

    private static void LogToDebugFile(Exception? ex)
    {
        try
        {
            LogHelper.Log($"[FATAL CRASH] {ex?.Message ?? "Unknown exception"}");
        }
        catch
        {
            // Ignore logging errors
        }
    }

    private static void ShowCrashDialog(string crashFile, Exception? ex)
    {
        MessageBox.Show(
            $"SACGUI has crashed!{Environment.NewLine}{Environment.NewLine}" +
            $"A crash report has been saved to:{Environment.NewLine}{crashFile}{Environment.NewLine}{Environment.NewLine}" +
            $"Error: {ex?.Message ?? "Unknown error"}",
            "Fatal Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static void ShowFallbackErrorDialog()
    {
        MessageBox.Show(
            "Fatal crash - unable to write crash log",
            "Critical Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
