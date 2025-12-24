namespace APPID;

internal static class ProcessManager
{
    public static void TerminateProcessesByName(string processName)
    {
        Process[] processList = Process.GetProcesses();

        foreach (Process process in processList)
        {
            if (process.ProcessName.Contains(processName, StringComparison.OrdinalIgnoreCase))
            {
                var info = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C WMIC PROCESS WHERE \"Name Like '%{processName}%'\" CALL Terminate 1>nul 2>nul",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(info);
            }
        }
    }
}
