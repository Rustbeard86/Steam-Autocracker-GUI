using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace APPID.Utilities.Network;

/// <summary>
///     Provides shared HttpClient instances with proper configuration.
/// </summary>
public static class HttpClientFactory
{
    private static readonly Lazy<HttpClient> DefaultClient = new(() => CreateClient());
    private static readonly Lazy<HttpClient> InsecureClient = new(() => CreateClient(true));

    /// <summary>
    ///     Gets a shared HttpClient instance with default settings.
    /// </summary>
    public static HttpClient Default => DefaultClient.Value;

    /// <summary>
    ///     Gets a shared HttpClient instance that bypasses SSL certificate validation.
    ///     Use only when connecting to trusted endpoints with self-signed certificates.
    /// </summary>
    public static HttpClient Insecure => InsecureClient.Value;

    /// <summary>
    ///     Creates a new HttpClient instance with optional certificate bypass.
    /// </summary>
    /// <param name="bypassCertificateValidation">If true, accepts all SSL certificates.</param>
    /// <param name="timeout">Optional timeout. Defaults to 100 seconds.</param>
    /// <returns>A new HttpClient instance.</returns>
    public static HttpClient CreateClient(bool bypassCertificateValidation = false, TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler();

        if (bypassCertificateValidation)
        {
            handler.ServerCertificateCustomValidationCallback = AcceptAllCertificates;
        }

        var client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(100) };

        client.DefaultRequestHeaders.Add("User-Agent", "SACGUI/2.3");

        return client;
    }

    /// <summary>
    ///     Creates an HttpClientHandler configured to bypass certificate validation.
    /// </summary>
    public static HttpClientHandler CreateInsecureHandler()
    {
        return new HttpClientHandler { ServerCertificateCustomValidationCallback = AcceptAllCertificates };
    }

    /// <summary>
    ///     Certificate validation callback that accepts all certificates.
    /// </summary>
    private static bool AcceptAllCertificates(
        HttpRequestMessage message,
        X509Certificate2? cert,
        X509Chain? chain,
        SslPolicyErrors errors) => true;
}
