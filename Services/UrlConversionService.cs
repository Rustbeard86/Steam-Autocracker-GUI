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
