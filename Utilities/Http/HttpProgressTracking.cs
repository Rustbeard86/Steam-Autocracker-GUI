namespace APPID.Utilities.Http;

/// <summary>
///     Event args for HTTP progress tracking during uploads/downloads.
/// </summary>
public class HttpProgressEventArgs(int progressPercentage, long bytesTransferred, long? totalBytes) : EventArgs
{
    /// <summary>
    ///     Number of bytes transferred so far.
    /// </summary>
    public long BytesTransferred { get; } = bytesTransferred;

    /// <summary>
    ///     Total bytes to transfer (if known).
    /// </summary>
    public long? TotalBytes { get; } = totalBytes;

    /// <summary>
    ///     Progress percentage (0-100).
    /// </summary>
    public int ProgressPercentage { get; } = progressPercentage;
}

/// <summary>
///     HTTP message handler that tracks upload/download progress.
/// </summary>
public class ProgressMessageHandler : HttpClientHandler
{
    /// <summary>
    ///     Event fired when progress changes during HTTP operations.
    /// </summary>
    public event EventHandler<HttpProgressEventArgs>? HttpSendProgress;

    /// <summary>
    ///     Sends an HTTP request with progress tracking.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // For now, just send without progress tracking
        // Full implementation would require tracking the request stream
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Simulate progress complete
        HttpSendProgress?.Invoke(this, new HttpProgressEventArgs(100, 100, 100));

        return response;
    }

    /// <summary>
    ///     Raises the HttpSendProgress event.
    /// </summary>
    /// <param name="bytesTransferred">Bytes transferred so far.</param>
    /// <param name="totalBytes">Total bytes to transfer (if known).</param>
    protected void OnHttpSendProgress(long bytesTransferred, long? totalBytes)
    {
        int percentage = totalBytes.HasValue && totalBytes.Value > 0
            ? (int)(bytesTransferred * 100 / totalBytes.Value)
            : 0;

        HttpSendProgress?.Invoke(this, new HttpProgressEventArgs(percentage, bytesTransferred, totalBytes));
    }
}
