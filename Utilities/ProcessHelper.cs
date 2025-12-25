namespace APPID.Utilities;

/// <summary>
///     Helper class for launching external processes, URLs, and files
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    ///     Opens a URL in the default browser
    /// </summary>
    /// <param name="url">The URL to open</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = url, UseShellExecute = true };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessHelper] Failed to open URL: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Opens a file or directory in Explorer
    /// </summary>
    /// <param name="path">The path to open</param>
    /// <param name="selectFile">If true, selects the file in Explorer instead of opening the folder</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool OpenInExplorer(string path, bool selectFile = false)
    {
        try
        {
            if (selectFile && File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (Directory.Exists(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessHelper] Failed to open in Explorer: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Launches an executable with optional arguments
    /// </summary>
    /// <param name="exePath">Path to the executable</param>
    /// <param name="arguments">Command line arguments</param>
    /// <param name="workingDirectory">Working directory for the process</param>
    /// <param name="createNoWindow">Whether to create a console window</param>
    /// <returns>The started Process, or null if failed</returns>
    public static Process? LaunchExecutable(string exePath, string arguments = "", string workingDirectory = "",
        bool createNoWindow = true)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath, Arguments = arguments, UseShellExecute = false, CreateNoWindow = createNoWindow
            };

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }

            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessHelper] Failed to launch executable: {ex.Message}");
            return null;
        }
    }
}
