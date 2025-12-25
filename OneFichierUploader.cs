using System.Net;
using APPID.Utilities.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APPID;

public class OneFichierUploader : IDisposable
{
    private const string ApiKey = "PSd6297MACENE2VQD7eNxBWIKrrmTTZb";
    private const string ApiBaseUrl = "https://api.1fichier.com/v1";
    private readonly HttpClient _httpClient;
    private readonly HttpClient _uploadClient; // Separate client for uploads with longer timeout
    private bool _disposed;

    public OneFichierUploader()
    {
        // Client for API calls
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer()
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SACGUI/1.0");

        // Separate client for uploads - no timeout for large files
        var uploadHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = true, CookieContainer = new CookieContainer()
        };
        _uploadClient = new HttpClient(uploadHandler);
        _uploadClient.Timeout = Timeout.InfiniteTimeSpan; // Critical for large files
        _uploadClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
        _uploadClient.DefaultRequestHeaders.Add("User-Agent", "SACGUI/1.0");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async Task<UploadServerInfo> GetUploadServerAsync()
    {
        try
        {
            // Use HttpClient instead of obsolete WebRequest
            using var client = HttpClientFactory.CreateClient(true);

            // Create request with required headers
            var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/upload/get_upload_server.cgi");
            request.Headers.Add("Authorization", $"Bearer {ApiKey}");
            request.Content = new StringContent("", Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            Debug.WriteLine($"[1FICHIER] Response Status: {response.StatusCode}");
            Debug.WriteLine($"[1FICHIER] Response: {json}");

            if (response.IsSuccessStatusCode)
            {
                var serverInfo = JsonConvert.DeserializeObject<UploadServerInfo>(json);
                Debug.WriteLine($"[1FICHIER] Got upload server: {serverInfo.Url}, ID: {serverInfo.Id}");
                return serverInfo;
            }

            throw new Exception($"Failed to get upload server: {response.StatusCode}. Response: {json}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[1FICHIER] Error getting upload server: {ex.Message}");
            throw;
        }
    }

    public async Task<UploadResult> UploadFileAsync(string filePath, IProgress<double> progressCallback = null,
        IProgress<string> statusCallback = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            var fileName = Path.GetFileName(filePath);
            var fileSize = fileInfo.Length;

            Debug.WriteLine(
                $"[1FICHIER] Uploading file: {fileName}, Size: {fileSize / (1024 * 1024)}MB ({fileSize / (1024.0 * 1024 * 1024):F2} GB)");

            // Step 1: Get upload server
            var serverInfo = await GetUploadServerAsync();

            // Step 2: Upload the file using HttpClient with custom streaming content
            var uploadUrl = $"https://{serverInfo.Url}/upload.cgi?id={serverInfo.Id}";
            Debug.WriteLine($"[1FICHIER] Upload URL: {uploadUrl}");
            Debug.WriteLine(
                "[1FICHIER] Using HttpClient with LargeFileMultipartContent for streaming (supports files > 2GB)");

            // Create boundary for multipart
            string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N");

            // Use custom HttpContent that streams without buffering
            using var content =
                new LargeFileMultipartContent(filePath, fileName, boundary, progressCallback, statusCallback);
            // Send request using HttpClient (not HttpWebRequest)
            // HttpClient with our custom content will NOT buffer because TryComputeLength returns true
            var response = await _uploadClient.PostAsync(uploadUrl, content, cancellationToken);

            statusCallback?.Report("Upload complete, waiting for server response...");

            Debug.WriteLine($"[1FICHIER] Upload response status: {response.StatusCode}");

            // Log response headers
            Debug.WriteLine("[1FICHIER] Response headers:");
            foreach (var header in response.Headers)
            {
                Debug.WriteLine($"[1FICHIER]   {header.Key}: {string.Join(", ", header.Value)}");
            }

            // Handle redirect (302/301)
            if (response.StatusCode == HttpStatusCode.Redirect ||
                response.StatusCode == HttpStatusCode.Found ||
                response.StatusCode == HttpStatusCode.MovedPermanently)
            {
                var location = response.Headers.Location?.ToString();
                Debug.WriteLine($"[1FICHIER] Upload complete! Redirected to: {location}");

                // Extract upload ID from location
                string uploadId = null;
                if (!string.IsNullOrEmpty(location) && location.Contains("xid="))
                {
                    var xidStart = location.IndexOf("xid=", StringComparison.Ordinal) + 4;
                    var xidEnd = location.IndexOf("&", xidStart, StringComparison.Ordinal);
                    if (xidEnd == -1)
                    {
                        xidEnd = location.Length;
                    }

                    uploadId = location.Substring(xidStart, xidEnd - xidStart);
                }

                if (!string.IsNullOrEmpty(uploadId))
                {
                    // Step 3: Get download links
                    var downloadInfo = await GetDownloadLinksAsync(serverInfo.Url, uploadId, statusCallback);

                    progressCallback?.Report(1.0); // Complete

                    return new UploadResult
                    {
                        DownloadUrl = downloadInfo?.DownloadUrl,
                        FileName = fileName,
                        FileSize = fileSize,
                        UploadId = uploadId
                    };
                }
            }
            else if (response.StatusCode == HttpStatusCode.OK)
            {
                // Sometimes 1fichier returns 200 OK with the file URL in the response
                var responseContent = await response.Content.ReadAsStringAsync();

                Debug.WriteLine(
                    $"[1FICHIER] Response body preview: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}");

                // Check if response contains a file URL
                var urlMatch = Regex.Match(responseContent, @"https://1fichier\.com/\?[\w]+");
                if (urlMatch.Success)
                {
                    var downloadUrl = urlMatch.Value;
                    Debug.WriteLine($"[1FICHIER] Found download URL in response: {downloadUrl}");

                    progressCallback?.Report(1.0); // Complete

                    return new UploadResult
                    {
                        DownloadUrl = downloadUrl, FileName = fileName, FileSize = fileSize, UploadId = ""
                    };
                }

                // Try to extract file ID from HTML
                var fileIdMatch = Regex.Match(responseContent, @"https://1fichier\.com/\?(\w+)");
                if (fileIdMatch is { Success: true, Groups.Count: > 1 })
                {
                    var fileId = fileIdMatch.Groups[1].Value;
                    var downloadUrl = $"https://1fichier.com/?{fileId}";
                    Debug.WriteLine($"[1FICHIER] Extracted file ID from HTML: {fileId}");

                    progressCallback?.Report(1.0); // Complete

                    return new UploadResult
                    {
                        DownloadUrl = downloadUrl, FileName = fileName, FileSize = fileSize, UploadId = fileId
                    };
                }

                // Check for French error message
                if (responseContent.Contains("Pas de fichier"))
                {
                    throw new Exception("Upload failed: No file was received by server");
                }

                throw new Exception("Upload succeeded but couldn't extract download URL from response");
            }

            // Handle error response
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Unexpected response: {response.StatusCode}, Content: {errorContent}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[1FICHIER] Upload error: {ex.Message}");
            Debug.WriteLine($"[1FICHIER] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task<DownloadInfo> GetDownloadLinksAsync(string serverUrl, string uploadId,
        IProgress<string> statusCallback = null)
    {
        // Retry for up to 5 minutes to wait for antivirus scan
        int maxRetries = 10; // 10 retries x 30 seconds = 5 minutes
        int retryDelay = 30000; // 30 seconds

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var endUrl = $"https://{serverUrl}/end.pl?xid={uploadId}";
                Debug.WriteLine($"[1FICHIER] Getting download links from: {endUrl} (attempt {attempt}/{maxRetries})");

                var request = new HttpRequestMessage(HttpMethod.Get, endUrl);
                request.Headers.Add("JSON", "1");

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Check if still waiting for antivirus scan
                    if (responseContent.Contains("Veuillez patienter") || responseContent.Contains("Please wait"))
                    {
                        statusCallback?.Report(
                            $"1fichier is still processing upload... retrying ({attempt}/{maxRetries})");
                        Debug.WriteLine(
                            $"[1FICHIER] File still being scanned by antivirus, waiting {retryDelay / 1000}s before retry...");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(retryDelay);
                            continue;
                        }

                        Debug.WriteLine($"[1FICHIER] Antivirus scan timeout after {maxRetries} attempts");
                        statusCallback?.Report("1fichier processing timed out");
                        return null;
                    }

                    if (contentType != null && contentType.Contains("application/json"))
                    {
                        var data = JObject.Parse(responseContent);
                        var links = data["links"]?.FirstOrDefault();

                        if (links != null)
                        {
                            var downloadUrl = links["download"]?.ToString();
                            Debug.WriteLine($"[1FICHIER] Download URL: {downloadUrl}");

                            return new DownloadInfo
                            {
                                DownloadUrl = downloadUrl,
                                FileName = links["filename"]?.ToString(),
                                Size = links["size"]?.ToObject<long>() ?? 0
                            };
                        }
                    }
                    else
                    {
                        // Try to parse HTML response for download link
                        Debug.WriteLine("[1FICHIER] Non-JSON response, trying to parse HTML");
                        // For now, construct a basic download URL
                        var downloadUrl = $"https://1fichier.com/?{uploadId}";
                        return new DownloadInfo { DownloadUrl = downloadUrl };
                    }
                }

                Debug.WriteLine($"[1FICHIER] Failed to get download links: {response.StatusCode}");

                // Retry on failure
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay);
                    continue;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"[1FICHIER] Error getting download links (attempt {attempt}/{maxRetries}): {ex.Message}");

                // Retry on exception
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay);
                    continue;
                }

                return null;
            }
        }

        return null;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
                _uploadClient?.Dispose();
            }

            _disposed = true;
        }
    }

    public class UploadServerInfo
    {
        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("id")] public string Id { get; set; }
    }

    public class UploadResult
    {
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string UploadId { get; set; }
    }

    private class DownloadInfo
    {
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
    }
}
