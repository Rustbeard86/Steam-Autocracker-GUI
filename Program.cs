using System.IO.Compression;
using System.Net;
using APPID;
using APPID.Properties;
using SteamAutocrackGUI;

namespace SteamAppIdIdentifier;

/// <summary>
///     Entry point for the SACGUI application.
/// </summary>
internal static class Program
{
    public static Mutex? Mutex { get; private set; }
    public static SteamAppId? Form { get; private set; }
    public static string[]? CommandLineArgs { get; private set; }

    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // Set up global exception handlers FIRST
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        // Bootstrap _bin folder if missing
        BootstrapBinFolder();

        // Extract embedded _bin files on first run
        ResourceExtractor.ExtractBinFiles();

        try
        {
            Mutex = new Mutex(false, "APPID.exe", out bool mutexCreated);

            if (!mutexCreated)
            {
                MessageBox.Show(
                    new Form { TopMost = true },
                    "Steam APPID finder is already running!",
                    "Already Running!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Mutex.Close();
                Application.Exit();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Initialize bandwidth limit from settings
            try
            {
                string bwLimit = Settings.Default.UploadBandwidthLimit ?? "";
                CompressionSettingsForm.ParseBandwidthLimit(bwLimit);
            }
            catch
            {
                /* Ignore bandwidth limit initialization errors */
            }

            Form = new SteamAppId();
            CommandLineArgs = args;
            Application.Run(Form);
            Application.Exit();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    private static void BootstrapBinFolder()
    {
        // Force use exe's actual directory
        string basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string binPath = Path.Combine(basePath, "_bin");
        string sevenZipPath = Path.Combine(binPath, "7z", "7za.exe");

        // If 7za.exe exists, _bin folder is good
        if (File.Exists(sevenZipPath))
        {
            return;
        }

        try
        {
            const string zipUrl = "https://share.harryeffingpotter.com/u/tattered-aidi.zip";
            string tempZip = Path.Combine(basePath, "_bin_download.zip");

            // Download the zip
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                var response = client.GetAsync(zipUrl).Result;
                response.EnsureSuccessStatusCode();
                var bytes = response.Content.ReadAsByteArrayAsync().Result;
                File.WriteAllBytes(tempZip, bytes);
            }

            // Extract using built-in .NET ZipFile
            ZipFile.ExtractToDirectory(tempZip, basePath, true);

            // Clean up
            File.Delete(tempZip);
        }
        catch (Exception ex)
        {
            // Log to file so we can see what went wrong
            try
            {
                string logPath = Path.Combine(basePath, "bootstrap_error.log");
                File.WriteAllText(logPath, $"Bootstrap failed: {ex}");
            }
            catch
            {
                /* Ignore logging errors */
            }
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        WriteCrashLog(e.ExceptionObject as Exception);

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e) =>
        WriteCrashLog(e.Exception);

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

            // Write crash.log next to exe for easy access
            string crashFile = Path.Combine(AppContext.BaseDirectory, "crash.log");
            File.WriteAllText(crashFile, crashInfo);

            // Also log to debug.log if possible
            try
            {
                LogHelper.Log($"[FATAL CRASH] {ex?.Message ?? "Unknown exception"}");
            }
            catch
            {
                /* Ignore logging errors */
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
            // Last resort - at least try to show something
            MessageBox.Show(
                "Fatal crash - unable to write crash log",
                "Critical Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    ///     Terminates all processes matching the specified name using WMIC.
    /// </summary>
    public static void CmdKILL(string appname)
    {
        Process[] processlist = Process.GetProcesses();
        foreach (Process process in processlist)
        {
            if (process.ProcessName.Contains(appname, StringComparison.OrdinalIgnoreCase))
            {
                var info = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C WMIC PROCESS WHERE \"Name Like '%{appname}%'\" CALL Terminate 1>nul 2>nul",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(info);
            }
        }
    }
}
