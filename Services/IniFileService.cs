using System.Diagnostics;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of INI file service for editing INI configuration files.
/// </summary>
public sealed class IniFileService : IIniFileService
{
    private readonly string _binPath;

    public IniFileService(string binPath)
    {
        _binPath = binPath;
    }

    public void EditIniFile(string args)
    {
        var iniProcess = new Process();
        iniProcess.StartInfo.CreateNoWindow = true;
        iniProcess.StartInfo.UseShellExecute = false;
        iniProcess.StartInfo.FileName = $"{_binPath}\\ALI213\\inifile.exe";
        iniProcess.StartInfo.Arguments = args;
        iniProcess.Start();
        iniProcess.WaitForExit();
    }
}
