using System.Net.NetworkInformation;

namespace APPID.Utilities;

/// <summary>
///     Handles internet connectivity checks.
/// </summary>
internal static class Updater
{
    private static readonly string[] DnsServers = ["1.1.1.1", "8.8.8.8", "208.67.222.222"];

    private static bool HasInternet { get; set; }
    private static bool IsOffline { get; set; }

    /// <summary>
    ///     Checks for internet connectivity by pinging multiple DNS servers.
    /// </summary>
    public static async Task<bool> CheckForNetAsync()
    {
        if (IsOffline)
        {
            return HasInternet;
        }

        foreach (string host in DnsServers)
        {
            if (await CheckForInternetAsync(host))
            {
                HasInternet = true;
                return true;
            }
        }

        if (!HasInternet)
        {
            IsOffline = true;
        }

        return HasInternet;
    }

    /// <summary>
    ///     Checks for internet connectivity by pinging a single host.
    /// </summary>
    private static async Task<bool> CheckForInternetAsync(string host)
    {
        try
        {
            using var myPing = new Ping();
            byte[] buffer = new byte[32];
            const int timeout = 20000;
            var pingOptions = new PingOptions();

            PingReply reply = await myPing.SendPingAsync(host, timeout, buffer, pingOptions);
            if (reply.Status == IPStatus.Success)
            {
                HasInternet = true;
                IsOffline = false;
                return true;
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogNetwork($"Internet check failed for {host}: {ex.Message}");
        }

        return false;
    }
}
