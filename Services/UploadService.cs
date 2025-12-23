using System.Net.Http;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
/// Implementation of file upload service for SACGUI backend.
/// </summary>
public sealed class UploadService : IUploadService
{
    private const string UploadEndpoint = "https://share.harryeffingpotter.com/upload";
    private const int MaxFileSizeMB = 500;
    private const double BytesToMB = 1024.0 * 1024.0;
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
            var fileSizeMB = fileInfo.Length / BytesToMB;
            
            LogHelper.Log($"[UPLOAD] Starting upload: {Path.GetFileName(filePath)} ({fileSizeMB:F2} MB)");

            if (fileSizeMB > MaxFileSizeMB)
            {
                LogHelper.Log($"[UPLOAD] WARNING: File is {fileSizeMB:F0}MB (exceeds {MaxFileSizeMB}MB limit)");
            }

            using var content = new MultipartFormDataContent();
            
            // Add file content
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
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
            else
            {
                LogHelper.Log($"[UPLOAD] FAILED - Status: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Upload failed", ex);
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
