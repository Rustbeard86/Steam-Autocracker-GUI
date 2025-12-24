using System.IO.Compression;

namespace APPID;

internal static class BootstrapService
{
    public static void EnsureBinFilesAvailable()
    {
        string basePath = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string binPath = Path.Combine(basePath, "_bin");
        string sevenZipPath = Path.Combine(binPath, "7z", "7za.exe");

        if (File.Exists(sevenZipPath))
        {
            return;
        }

        try
        {
            DownloadAndExtractBinFiles(basePath);
        }
        catch (Exception ex)
        {
            LogBootstrapError(basePath, ex);
        }
    }

    private static void DownloadAndExtractBinFiles(string basePath)
    {
        const string zipUrl = "https://share.harryeffingpotter.com/u/tattered-aidi.zip";
        string tempZip = Path.Combine(basePath, "_bin_download.zip");

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        {
            var response = client.GetAsync(zipUrl).Result;
            response.EnsureSuccessStatusCode();

            var bytes = response.Content.ReadAsByteArrayAsync().Result;
            File.WriteAllBytes(tempZip, bytes);
        }

        ZipFile.ExtractToDirectory(tempZip, basePath, true);
        File.Delete(tempZip);
    }

    private static void LogBootstrapError(string basePath, Exception ex)
    {
        try
        {
            string logPath = Path.Combine(basePath, "bootstrap_error.log");
            File.WriteAllText(logPath, $"Bootstrap failed: {ex}");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}
