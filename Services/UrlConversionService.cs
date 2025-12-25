using System.Text.Json;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of URL conversion service for 1fichier to PyDrive conversions.
/// </summary>
public sealed class UrlConversionService : IUrlConversionService
{
    private const string ConversionApiUrl = "https://pydrive.harryeffingpotter.com/convert-1fichier";
    private const int MaxRetries = 30;
    private const int BaseRetryDelaySeconds = 10;
    private const int MaxRetryDelaySeconds = 60;

    public async Task<string?> ConvertOneFichierToPyDriveAsync(
        string oneFichierUrl,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Force HTTPS
        if (oneFichierUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            oneFichierUrl = "https://" + oneFichierUrl.Substring(7);
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new { link = oneFichierUrl };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                statusCallback?.Invoke($"Converting... (attempt {attempt}/{MaxRetries})");
                LogHelper.Log($"[URL_CONVERT] Attempting conversion (attempt {attempt}/{MaxRetries}): {oneFichierUrl}");

                var response = await client.PostAsync(ConversionApiUrl, content, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var jsonDoc = JsonDocument.Parse(responseJson);

                    if (jsonDoc.RootElement.TryGetProperty("link", out var linkProperty))
                    {
                        var convertedUrl = linkProperty.GetString();
                        LogHelper.Log($"[URL_CONVERT] Successfully converted: {convertedUrl}");
                        return convertedUrl;
                    }
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    LogHelper.Log($"[URL_CONVERT] Failed with status {response.StatusCode}: {responseContent}");

                    // Check if it's a "still processing" error
                    if ((responseContent.Contains("LINK_DOWN") || responseContent.Contains("wait"))
                        && attempt < MaxRetries)
                    {
                        // Variable delay - increases with each attempt
                        int delaySec = Math.Min(BaseRetryDelaySeconds + (attempt * 2), MaxRetryDelaySeconds);

                        // Countdown display
                        for (int i = delaySec; i > 0 && !cancellationToken.IsCancellationRequested; i--)
                        {
                            statusCallback?.Invoke(
                                $"1fichier scanning... retry in {i}s (attempt {attempt}/{MaxRetries})");
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        }

                        continue;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogHelper.Log("[URL_CONVERT] Conversion cancelled");
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[URL_CONVERT] Exception during conversion (attempt {attempt}/{MaxRetries})", ex);

                if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
                {
                    int delaySec = BaseRetryDelaySeconds;
                    for (int i = delaySec; i > 0 && !cancellationToken.IsCancellationRequested; i--)
                    {
                        statusCallback?.Invoke($"Error, retry in {i}s (attempt {attempt}/{MaxRetries})");
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }
            }

            // Wait before next retry if not the last attempt
            if (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                int delaySec = BaseRetryDelaySeconds;
                for (int i = delaySec; i > 0 && !cancellationToken.IsCancellationRequested; i--)
                {
                    statusCallback?.Invoke($"Retry in {i}s (attempt {attempt}/{MaxRetries})");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        LogHelper.Log("[URL_CONVERT] All retry attempts exhausted, returning original URL");
        // If conversion fails after all retries, return original link
        return oneFichierUrl;
    }

    public async Task<string?> ConvertOneFichierToPyDriveAsync(
        string oneFichierUrl,
        long fileSizeBytes,
        Action<string>? statusCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Normalize URL to HTTPS
        if (oneFichierUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            oneFichierUrl = "https://" + oneFichierUrl.Substring(7);
        }

        // Calculate wait time based on file size (12 seconds per GB, capped at 30 hours)
        int initialWaitSeconds = 30;
        if (fileSizeBytes > 5L * 1024 * 1024 * 1024) // 5GB+
        {
            long sizeInGb = fileSizeBytes / (1024 * 1024 * 1024);
            initialWaitSeconds = (int)(sizeInGb * 12);
            initialWaitSeconds = Math.Min(initialWaitSeconds, 108000); // Cap at 30 hours
            initialWaitSeconds = Math.Max(initialWaitSeconds, 30);
        }

        // Calculate retries: enough to cover estimated wait time plus buffer
        int maxRetries = Math.Max(10, (initialWaitSeconds / 30) + 5);
        int retryDelay = 30000; // 30 seconds

        double sizeGb = fileSizeBytes / (1024.0 * 1024.0 * 1024.0);
        LogHelper.Log($"[CONVERT] Starting conversion for {oneFichierUrl}");
        LogHelper.Log(
            $"[CONVERT] File size: {sizeGb:F2} GB, Initial wait: {initialWaitSeconds}s, Max retries: {maxRetries}");

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            try
            {
                int remainingSeconds = Math.Max(0, initialWaitSeconds - ((attempt - 1) * 30));
                string timeStr = remainingSeconds > 60 ? $"~{remainingSeconds / 60}m" : $"~{remainingSeconds}s";
                statusCallback?.Invoke($"Converting {attempt}/{maxRetries} ({timeStr})");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestBody = new { link = oneFichierUrl };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client
                    .PostAsync(ConversionApiUrl, content, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson =
                        await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("link", out var linkProp))
                    {
                        string? pydriveUrl = linkProp.GetString();
                        LogHelper.Log($"[CONVERT] SUCCESS! PyDrive URL: {pydriveUrl}");
                        return pydriveUrl;
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    // Check if it's a "still processing" error - keep retrying
                    if (errorBody.Contains("LINK_DOWN") || errorBody.Contains("wait"))
                    {
                        LogHelper.Log("[CONVERT] 1fichier still scanning file, waiting...");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[CONVERT] Exception on attempt {attempt}", ex);
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        LogHelper.Log($"[CONVERT] FAILED after {maxRetries} attempts, will use 1fichier link");
        return null; // Conversion failed, will use 1fichier link
    }

    public bool IsOneFichierUrl(string url)
    {
        return !string.IsNullOrEmpty(url) &&
               url.Contains("1fichier.com", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPyDriveUrl(string url)
    {
        return !string.IsNullOrEmpty(url) &&
               url.Contains("pydrive.harryeffingpotter.com", StringComparison.OrdinalIgnoreCase);
    }
}
