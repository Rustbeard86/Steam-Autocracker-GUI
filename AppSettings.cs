using Newtonsoft.Json;

namespace APPID;

/// <summary>
///     Application settings stored in JSON format at %AppData%\SACGUI\settings.json
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SACGUI",
        "settings.json");

    // Settings properties with XML documentation

    /// <summary>Gets or sets the last selected directory.</summary>
    public string LastDir { get; set; } = string.Empty;

    /// <summary>Gets or sets whether to use Goldberg emulator.</summary>
    public bool Goldy { get; set; } = true;

    /// <summary>Gets or sets whether the window is pinned on top.</summary>
    public bool Pinned { get; set; } = true;

    /// <summary>Gets or sets whether auto-crack is enabled.</summary>
    public bool AutoCrack { get; set; }

    /// <summary>Gets or sets whether LAN multiplayer is enabled.</summary>
    public bool LanMultiplayer { get; set; }

    /// <summary>Gets or sets whether to use RIN password for archives.</summary>
    public bool UseRinPassword { get; set; }

    /// <summary>Gets or sets shared games data.</summary>
    public string SharedGamesData { get; set; } = string.Empty;

    /// <summary>Gets or sets the compression format (zip or 7z).</summary>
    public string ZipFormat { get; set; } = "zip";

    /// <summary>Gets or sets the compression level.</summary>
    public string ZipLevel { get; set; } = "Normal";

    /// <summary>Gets or sets whether to skip compression confirmation dialog.</summary>
    public bool ZipDontAsk { get; set; }

    /// <summary>
    ///     Gets the singleton instance of AppSettings.
    /// </summary>
    public static AppSettings Default
    {
        get
        {
            field ??= Load();
            return field;
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Failed to load settings", ex);
        }

        return new AppSettings();
    }

    /// <summary>
    ///     Saves the current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Failed to save settings", ex);
        }
    }
}
