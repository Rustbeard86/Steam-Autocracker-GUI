namespace APPID;

internal static class Program
{
    public static SteamAppId? Form { get; private set; }
    public static string[]? CommandLineArgs { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        ExceptionHandler.ConfigureGlobalHandlers();

        BootstrapService.EnsureBinFilesAvailable();
        ResourceExtractor.ExtractBinFiles();

        if (!SingleInstanceManager.EnsureSingleInstance())
        {
            return;
        }

        try
        {
            InitializeApplication();
            SettingsInitializer.Initialize();

            Form = new SteamAppId();
            CommandLineArgs = args;
            Application.Run(Form);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
        finally
        {
            Application.Exit();
        }
    }

    private static void InitializeApplication()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }

    private static void WriteCrashLog(Exception? ex)
    {
        try
        {
            string crashInfo = $"""
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

            string crashFile = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.WriteAllText(crashFile, crashInfo);

            try
            {
                LogHelper.Log($"[FATAL CRASH] {ex?.Message ?? "Unknown exception"}");
            }
            catch
            {
            }

            MessageBox.Show(
                $"SACGUI has crashed!{Environment.NewLine}{Environment.NewLine}" +
                $"A crash report has been saved to:{Environment.NewLine}{crashFile}{Environment.NewLine}{Environment.NewLine}" +
                $"Error: {ex?.Message ?? "Unknown error"}",
                "Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            MessageBox.Show(
                "Fatal crash - unable to write crash log",
                "Critical Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
