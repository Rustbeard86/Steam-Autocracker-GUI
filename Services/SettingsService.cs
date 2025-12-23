using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of settings service that wraps AppSettings singleton.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    public string LastDir
    {
        get => AppSettings.Default.LastDir;
        set => AppSettings.Default.LastDir = value;
    }

    public bool Goldy
    {
        get => AppSettings.Default.Goldy;
        set => AppSettings.Default.Goldy = value;
    }

    public bool Pinned
    {
        get => AppSettings.Default.Pinned;
        set => AppSettings.Default.Pinned = value;
    }

    public bool AutoCrack
    {
        get => AppSettings.Default.AutoCrack;
        set => AppSettings.Default.AutoCrack = value;
    }

    public bool LANMultiplayer
    {
        get => AppSettings.Default.LANMultiplayer;
        set => AppSettings.Default.LANMultiplayer = value;
    }

    public bool UseRinPassword
    {
        get => AppSettings.Default.UseRinPassword;
        set => AppSettings.Default.UseRinPassword = value;
    }

    public string SharedGamesData
    {
        get => AppSettings.Default.SharedGamesData;
        set => AppSettings.Default.SharedGamesData = value;
    }

    public string ZipFormat
    {
        get => AppSettings.Default.ZipFormat;
        set => AppSettings.Default.ZipFormat = value;
    }

    public string ZipLevel
    {
        get => AppSettings.Default.ZipLevel;
        set => AppSettings.Default.ZipLevel = value;
    }

    public bool ZipDontAsk
    {
        get => AppSettings.Default.ZipDontAsk;
        set => AppSettings.Default.ZipDontAsk = value;
    }

    public void Save()
    {
        AppSettings.Default.Save();
    }
}
