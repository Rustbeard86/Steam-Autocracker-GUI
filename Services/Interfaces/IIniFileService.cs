namespace APPID.Services.Interfaces;

/// <summary>
///     Service for INI file editing operations.
/// </summary>
public interface IIniFileService
{
    /// <summary>
    ///     Edits an INI file using the inifile.exe utility.
    /// </summary>
    /// <param name="args">Arguments to pass to the inifile.exe utility.</param>
    void EditIniFile(string args);
}
