using System.Net.Http.Headers;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of file upload service for SACGUI backend.
/// </summary>
public sealed class UploadService : IUploadService
{
    private const string UploadEndpoint = "https://share.harryeffingpotter.com/upload";
    private const int MaxFileSizeMb = 500;
    private const double BytesToMb = 1024.0 * 1024.0;
    private const string AppVersion = "SACGUI-2.3";

    // Reuse HttpClient to avoid socket exhaustion
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromHours(2) };
    private static readonly HttpClient IpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<string?> UploadFileAsync(string filePath, string gameName, Action<int>? progressCallback = null)
    {
        if (!File.Exists(filePath))
        {
            LogHelper.LogError("Upload failed: File not found", null);
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileSizeMb = fileInfo.Length / BytesToMb;

            LogHelper.Log($"[UPLOAD] Starting upload: {Path.GetFileName(filePath)} ({fileSizeMb:F2} MB)");

            if (fileSizeMb > MaxFileSizeMb)
            {
                LogHelper.Log($"[UPLOAD] WARNING: File is {fileSizeMb:F0}MB (exceeds {MaxFileSizeMb}MB limit)");
            }

            using var content = new MultipartFormDataContent();

            // Add file content
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            // Add metadata
            content.Add(new StringContent("anonymous"), "hwid");
            content.Add(new StringContent(AppVersion), "version");
            content.Add(new StringContent(gameName), "game_name");

            // Get client IP
            string clientIp = await GetPublicIpAsync() ?? "127.0.0.1";
            content.Add(new StringContent(clientIp), "client_ip");

            LogHelper.Log($"[UPLOAD] Uploading to backend... Game: {gameName}");

            // Upload with progress tracking
            var response = await SharedClient.PostAsync(UploadEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                LogHelper.Log($"[UPLOAD] SUCCESS - Response: {result}");
                progressCallback?.Invoke(100);
                return result;
            }

            LogHelper.Log($"[UPLOAD] FAILED - Status: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Upload failed", ex);
            return null;
        }
    }

    public async Task<string?> UploadToOneFichierAsync(
        string filePath,
        IProgress<(long bytesTransferred, long totalBytes, double speed)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            LogHelper.LogError("[UPLOAD] File not found", null);
            return null;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            long totalBytes = fileInfo.Length;
            long bytesTransferred = 0;
            DateTime lastUpdate = DateTime.Now;
            double smoothedSpeed = 0;

            LogHelper.Log(
                $"[UPLOAD] Starting 1fichier upload: {Path.GetFileName(filePath)} ({totalBytes / BytesToMb:F2} MB)");

            using var uploader = new OneFichierUploader();

            var uploadProgress = new Progress<double>(percentComplete =>
            {
                long currentBytes = (long)(percentComplete * totalBytes);
                long bytesDelta = currentBytes - bytesTransferred;
                double timeDelta = (DateTime.Now - lastUpdate).TotalSeconds;

                if (timeDelta > 0.1 && bytesDelta > 0)
                {
                    double currentSpeed = bytesDelta / timeDelta;
                    smoothedSpeed = smoothedSpeed > 0
                        ? smoothedSpeed * 0.7 + currentSpeed * 0.3
                        : currentSpeed;

                    progress?.Report((currentBytes, totalBytes, smoothedSpeed));

                    bytesTransferred = currentBytes;
                    lastUpdate = DateTime.Now;
                }
            });

            var result = await uploader.UploadFileAsync(filePath, uploadProgress, null, cancellationToken)
                .ConfigureAwait(false);

            if (result != null && !string.IsNullOrEmpty(result.DownloadUrl))
            {
                LogHelper.Log($"[UPLOAD] SUCCESS - 1fichier URL: {result.DownloadUrl}");
                return result.DownloadUrl;
            }

            LogHelper.Log("[UPLOAD] FAILED - No URL returned from 1fichier");
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"[UPLOAD] Failed to upload {Path.GetFileName(filePath)}", ex);
            return null;
        }
    }

    private static async Task<string?> GetPublicIpAsync()
    {
        try
        {
            string ip = await IpClient.GetStringAsync("https://api.ipify.org");
            return ip?.Trim();
        }
        catch
        {
            return null;
        }
    }
}
