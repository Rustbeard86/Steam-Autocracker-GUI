namespace APPID.Utilities;

/// <summary>
///     Contains constants used during the cracking process to improve efficiency and accuracy.
/// </summary>
public static class CrackConstants
{
    /// <summary>
    ///     Maximum number of steam_api DLLs expected in a normal game installation.
    ///     If more are found, it may indicate incorrect folder selection.
    /// </summary>
    public const int MaxExpectedSteamApiDlls = 2;

    /// <summary>
    ///     Utility executables that should be skipped during Steamless processing.
    ///     These are support files, not game executables, and don't need unpacking.
    /// </summary>
    public static readonly string[] ExcludedExecutables =
    [
        // Unity Engine utilities
        "UnityCrashHandler64.exe",
        "UnityCrashHandler32.exe",
        "UnityCrashHandler.exe",

        // Unreal Engine utilities
        "UnrealCEFSubProcess.exe",
        "CrashReportClient.exe",
        "CrashReporter.exe",

        // Common game engine crash reporters
        "CrashSender.exe",
        "CrashSender1403.exe",
        "WerFault.exe",
        "crashpad_handler.exe",
        "ReportCrash.exe",
        "ErrorReporter.exe",

        // Installers and uninstallers
        "unins000.exe",
        "unins001.exe",
        "uninstall.exe",
        "setup.exe",
        "installer.exe",

        // Updaters and launchers (often separate from main game)
        "Launcher.exe",
        "LauncherPatcher.exe",
        "update.exe",
        "updater.exe",
        "patcher.exe",

        // Redistributables
        "vcredist_x64.exe",
        "vcredist_x86.exe",
        "DirectXSetup.exe",
        "UEPrereqSetup_x64.exe",
        "EpicOnlineServicesInstaller.exe",

        // Anti-cheat (should not be modified)
        "EasyAntiCheat.exe",
        "BEService.exe",
        "BattlEye.exe"
    ];

    /// <summary>
    ///     Common file patterns for Steam API DLLs.
    /// </summary>
    public static readonly string[] SteamApiDllPatterns =
    [
        "steam_api.dll",
        "steam_api64.dll"
    ];

    /// <summary>
    ///     Checks if an executable should be excluded from Steamless processing.
    /// </summary>
    /// <param name="fileName">The name of the executable file.</param>
    /// <returns>True if the file should be excluded, false otherwise.</returns>
    public static bool IsExcludedExecutable(string fileName)
    {
        return ExcludedExecutables.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }
}
