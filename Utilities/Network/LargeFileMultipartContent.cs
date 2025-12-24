using System.Net;
using System.Net.Http.Headers;

namespace APPID.Utilities.Network;

/// <summary>
///     Custom HttpContent that streams large files without buffering.
///     This is critical for files > 2GB which would otherwise cause "Stream was too long" errors.
/// </summary>
public class LargeFileMultipartContent : HttpContent
{
    // Track concurrent uploads for bandwidth limiting
    private static int _concurrentUploads;
    private static readonly Lock UploadCountLock = new();
    private readonly string _boundary;
    private readonly string _filePath;
    private readonly long _fileSize;
    private readonly byte[] _footerBytes;
    private readonly byte[] _headerBytes;
    private readonly IProgress<double> _progressCallback;
    private readonly IProgress<string> _statusCallback;

    public LargeFileMultipartContent(string filePath, string fileName, string boundary,
        IProgress<double> progressCallback = null, IProgress<string> statusCallback = null)
    {
        _filePath = filePath;
        _boundary = boundary;
        _fileSize = new FileInfo(filePath).Length;
        _progressCallback = progressCallback;
        _statusCallback = statusCallback;

        // Build header bytes (domain field + file header)
        var sb = new StringBuilder();
        sb.Append($"--{boundary}\r\n");
        sb.Append("Content-Disposition: form-data; name=\"domain\"\r\n\r\n");
        sb.Append("0\r\n");
        sb.Append($"--{boundary}\r\n");
        sb.Append($"Content-Disposition: form-data; name=\"file[]\"; filename=\"{fileName}\"\r\n");
        sb.Append("Content-Type: application/octet-stream\r\n\r\n");
        _headerBytes = Encoding.UTF8.GetBytes(sb.ToString());

        // Build footer bytes
        _footerBytes = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");

        // Set content type header
        Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
        Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", boundary));
    }

    /// <summary>
    ///     Returns true with pre-calculated length to prevent HttpClient from buffering.
    ///     This is the KEY to supporting large files - when this returns true, HttpClient
    ///     sets Content-Length header and streams directly without buffering.
    /// </summary>
    protected override bool TryComputeLength(out long length)
    {
        length = _headerBytes.Length + _fileSize + _footerBytes.Length;
        return true;
    }

    /// <summary>
    ///     Streams content directly to the network without buffering.
    /// </summary>
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        // Track concurrent uploads
        lock (UploadCountLock) { _concurrentUploads++; }

        Debug.WriteLine($"[1FICHIER] Upload started. Concurrent uploads: {_concurrentUploads}");

        try
        {
            // Write header
            await stream.WriteAsync(_headerBytes, 0, _headerBytes.Length);

            // Stream file content with progress reporting
            await using (var fileStream =
                         new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920))
            {
                var buffer = new byte[81920]; // 80KB buffer
                long totalWritten = 0;
                int bytesRead;

                var startTime = DateTime.Now;
                var lastUpdateTime = DateTime.Now;
                var lastThrottleTime = DateTime.Now;
                long lastBytesWritten = 0;
                long bytesThisSecond = 0;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                    totalWritten += bytesRead;
                    bytesThisSecond += bytesRead;

                    // Bandwidth throttling - check every write
                    long bandwidthLimit = CompressionSettingsForm.UploadBandwidthLimitBytesPerSecond;
                    if (bandwidthLimit > 0)
                    {
                        int currentUploads;
                        lock (UploadCountLock) { currentUploads = _concurrentUploads; }

                        long perUploadLimit = bandwidthLimit / Math.Max(1, currentUploads);

                        var throttleElapsed = (DateTime.Now - lastThrottleTime).TotalSeconds;
                        if (throttleElapsed > 0)
                        {
                            double currentRate = bytesThisSecond / throttleElapsed;
                            if (currentRate > perUploadLimit)
                            {
                                // Calculate how long to sleep to achieve target rate
                                double targetTime = (double)bytesThisSecond / perUploadLimit;
                                double sleepMs = (targetTime - throttleElapsed) * 1000;
                                if (sleepMs > 10)
                                {
                                    await Task.Delay((int)Math.Min(sleepMs, 500)); // Cap at 500ms
                                }
                            }
                        }

                        // Reset throttle counter every second
                        if (throttleElapsed >= 1.0)
                        {
                            lastThrottleTime = DateTime.Now;
                            bytesThisSecond = 0;
                        }
                    }

                    // Calculate and report progress/speed every 0.5 seconds
                    var now = DateTime.Now;
                    var timeSinceLastUpdate = (now - lastUpdateTime).TotalSeconds;

                    if (timeSinceLastUpdate >= 0.5)
                    {
                        var bytesThisInterval = totalWritten - lastBytesWritten;
                        var speedMBps = bytesThisInterval / (1024.0 * 1024.0) / timeSinceLastUpdate;
                        var totalElapsed = (now - startTime).TotalSeconds;
                        var avgSpeedMBps = totalElapsed > 0 ? totalWritten / (1024.0 * 1024.0) / totalElapsed : 0;
                        var bytesRemaining = _fileSize - totalWritten;
                        var etaSeconds = avgSpeedMBps > 0 ? bytesRemaining / (1024.0 * 1024.0) / avgSpeedMBps : 0;

                        var statusText =
                            $"Uploading: {speedMBps:F2} MB/s (Avg: {avgSpeedMBps:F2} MB/s) - ETA: {TimeSpan.FromSeconds(etaSeconds):hh\\:mm\\:ss}";
                        Debug.WriteLine($"[1FICHIER] {statusText}");
                        _statusCallback?.Report(statusText);

                        lastUpdateTime = now;
                        lastBytesWritten = totalWritten;
                    }

                    // Report progress (0.0 to 1.0)
                    _progressCallback?.Report((double)totalWritten / _fileSize);
                }

                Debug.WriteLine($"[1FICHIER] Streamed {totalWritten} bytes to network");
            }

            // Write footer
            await stream.WriteAsync(_footerBytes, 0, _footerBytes.Length);
        }
        finally
        {
            lock (UploadCountLock) { _concurrentUploads = Math.Max(0, _concurrentUploads - 1); }

            Debug.WriteLine($"[1FICHIER] Upload finished. Concurrent uploads: {_concurrentUploads}");
        }
    }
}
