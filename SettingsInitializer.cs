using APPID.Properties;

namespace APPID;

internal static class SettingsInitializer
{
    public static void Initialize()
    {
        try
        {
            string bwLimit = Settings.Default.UploadBandwidthLimit ?? "";
            CompressionSettingsForm.ParseBandwidthLimit(bwLimit);
        }
        catch
        {
            // Bandwidth limit initialization is optional
        }
    }
}
