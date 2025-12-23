namespace APPID.Services.Interfaces;

/// <summary>
///     Service for managing application settings with an abstraction layer.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets or sets the last selected directory.</summary>
    string LastDir { get; set; }

    /// <summary>Gets or sets whether to use Goldberg emulator.</summary>
    bool Goldy { get; set; }

    /// <summary>Gets or sets whether the window is pinned on top.</summary>
    bool Pinned { get; set; }

    /// <summary>Gets or sets whether auto-crack is enabled.</summary>
    bool AutoCrack { get; set; }

    /// <summary>Gets or sets whether LAN multiplayer is enabled.</summary>
    bool LANMultiplayer { get; set; }

    /// <summary>Gets or sets whether to use RIN password for archives.</summary>
    bool UseRinPassword { get; set; }

    /// <summary>Gets or sets shared games data.</summary>
    string SharedGamesData { get; set; }

    /// <summary>Gets or sets the compression format (zip or 7z).</summary>
    string ZipFormat { get; set; }

    /// <summary>Gets or sets the compression level.</summary>
    string ZipLevel { get; set; }

    /// <summary>Gets or sets whether to skip compression confirmation dialog.</summary>
    bool ZipDontAsk { get; set; }

    /// <summary>
    ///     Saves the current settings to disk.
    /// </summary>
    void Save();
}
