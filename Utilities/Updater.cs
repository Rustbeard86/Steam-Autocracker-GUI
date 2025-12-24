using System.Net.NetworkInformation;
using APPID.Utilities;
using Newtonsoft.Json;
using Octokit;
using RestSharp;
using FileMode = System.IO.FileMode;

namespace APPID;

/// <summary>
///     Handles checking for and downloading updates for external dependencies like Steamless and GoldBerg emulator.
/// </summary>
internal static class Updater
{
    private static readonly string[] DnsServers = ["1.1.1.1", "8.8.8.8", "208.67.222.222"];

    private static readonly string BinPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory,
        "_bin");

    public static bool HasInternet { get; private set; }
    public static bool IsOffline { get; private set; }

    /// <summary>
    ///     Fetches JSON data from GitHub API.
    /// </summary>
    private static dynamic? GetJson(string requestURL)
    {
        // Certificate validation is now handled by RestClient options
        try
        {
            string baseURL = "https://api.github.com/repos/";
            var options = new RestClientOptions(baseURL)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };

            var client = new RestClient(options);
            Debug.WriteLine($"Requesting: {baseURL}{requestURL}");

            var request = new RestRequest(requestURL);
            RestResponse queryResult = client.Execute(request);

            return queryResult.Content is not null
                ? JsonConvert.DeserializeObject<dynamic>(queryResult.Content)
                : null;
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"getJson failed for {requestURL}", ex);
            return null;
        }
    }

    /// <summary>
    ///     Checks for internet connectivity by pinging multiple DNS servers.
    /// </summary>
    public static async Task<bool> CheckForNetAsync()
    {
        // Certificate validation is now handled per-HttpClient via HttpClientFactory
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
    ///     Checks for newer version of Steamless on GitHub and updates if available.
    /// </summary>
    public static async Task CheckGitHubNewerVersion(string user, string repo, string apiBase)
    {
        try
        {
            // Certificate validation is now handled per-HttpClient via HttpClientFactory

            dynamic? obj = GetJson($"{user}/{repo}/releases");
            if (obj is null)
            {
                return;
            }

            var client = new GitHubClient(new ProductHeaderValue(repo));
            IReadOnlyList<Release> releases = await client.Repository.Release.GetAll(user, repo);

            if (releases.Count == 0)
            {
                return;
            }

            string latestGitHubVersion = releases[0].TagName.Replace("v", "");
            string versionFilePath = Path.Combine(BinPath, $"{repo}.ver");

            string localVersion = File.Exists(versionFilePath)
                ? File.ReadAllText(versionFilePath).Trim()
                : string.Empty;

            if (localVersion == latestGitHubVersion)
            {
                return; // Already up to date
            }

            LogHelper.LogUpdate("Steamless", $"{localVersion} -> {latestGitHubVersion}");

            // Delete old version
            string steamlessPath = Path.Combine(BinPath, "Steamless");
            if (Directory.Exists(steamlessPath))
            {
                Directory.Delete(steamlessPath, true);
            }

            // Find download URL
            foreach (var item in obj[0])
            {
                string itemStr = item.ToString();
                if (!itemStr.Contains("browser_download_url"))
                {
                    continue;
                }

                string downloadUrl = StringTools.RemoveEverythingBeforeFirstRemoveString(
                    item.Value.ToString(),
                    "browser_download_url\": \"");
                downloadUrl = StringTools.RemoveEverythingAfterFirstRemoveString(downloadUrl, "\"");

                // Download the new version using HttpClient
                using var httpClient = HttpClientFactory.CreateClient();
                HttpResponseMessage response = await httpClient.GetAsync(downloadUrl);
                await using (FileStream fs = new("SLS.zip", FileMode.CreateNew))
                {
                    await response.Content.CopyToAsync(fs);
                }

                // Extract and update
                await ExtractFileAsync("SLS.zip", steamlessPath);
                File.Delete("SLS.zip");
                File.Delete(versionFilePath);
                await File.WriteAllTextAsync(versionFilePath, latestGitHubVersion);
                break;
            }
        }
        catch (Exception ex)
        {
            // Silently ignore rate limit and other API errors - not critical
            LogHelper.LogNetwork($"GitHub API check skipped: {ex.Message}");
        }
    }

    /// <summary>
    ///     Checks for and downloads updates for GoldBerg Steam emulator.
    /// </summary>
    public static async Task UpdateGoldBergAsync()
    {
        try
        {
            // Certificate validation is now handled per-HttpClient via HttpClientFactory

            // Get latest release from the fork
            dynamic? obj = GetJson("Detanup01/gbe_fork/releases/latest");
            if (obj is null)
            {
                MessageBox.Show("Unable to check for GoldBerg updates. Please try again later.");
                return;
            }

            string latestVersion = obj.tag_name.ToString().Replace("release-", "");
            string versionFile = Path.Combine(BinPath, "Goldberg", "version.txt");

            string localVersion = File.Exists(versionFile)
                ? File.ReadAllText(versionFile).Trim()
                : string.Empty;

            if (localVersion == latestVersion)
            {
                return; // Already up to date
            }

            LogHelper.LogUpdate("GoldBerg (gbe_fork)", $"{localVersion} -> {latestVersion}");

            // Find the Windows release asset
            string? downloadUrl = null;
            foreach (var asset in obj.assets)
            {
                if (asset.name.ToString() == "emu-win-release.7z")
                {
                    downloadUrl = asset.browser_download_url.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show("Windows release asset not found for GoldBerg fork.");
                return;
            }

            // Download the new version using HttpClient
            using var httpClient = HttpClientFactory.CreateClient();
            string tempFile = "gbe_fork.7z";
            HttpResponseMessage response = await httpClient.GetAsync(downloadUrl);
            await using (FileStream fs = new(tempFile, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fs);
            }

            // Extract to temporary directory
            string tempDir = Path.Combine(BinPath, "temp_goldberg");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            await ExtractFileAsync(tempFile, tempDir);

            // Copy the DLL files to Goldberg directory
            string goldbergDir = Path.Combine(BinPath, "Goldberg");
            Directory.CreateDirectory(goldbergDir);

            // Copy the specific DLL files from known paths
            CopyIfExists(
                Path.Combine(tempDir, "release", "regular", "x64", "steam_api64.dll"),
                Path.Combine(goldbergDir, "steam_api64.dll"));

            CopyIfExists(
                Path.Combine(tempDir, "release", "regular", "x32", "steam_api.dll"),
                Path.Combine(goldbergDir, "steam_api.dll"));

            // Copy generate_interfaces tools if they exist
            string[] possibleToolPaths =
            [
                Path.Combine(tempDir, "release", "tools", "generate_interfaces", "generate_interfaces_x64.exe"),
                Path.Combine(tempDir, "release", "tools", "generate_interfaces", "generate_interfaces_x32.exe"),
                Path.Combine(tempDir, "tools", "generate_interfaces", "generate_interfaces_x64.exe"),
                Path.Combine(tempDir, "tools", "generate_interfaces", "generate_interfaces_x32.exe"),
                Path.Combine(tempDir, "generate_interfaces_x64.exe"),
                Path.Combine(tempDir, "generate_interfaces_x32.exe")
            ];

            foreach (string toolPath in possibleToolPaths.Where(File.Exists))
            {
                string fileName = Path.GetFileName(toolPath);
                string destPath = Path.Combine(goldbergDir, fileName);
                File.Copy(toolPath, destPath, true);
                Debug.WriteLine($"Copied tool: {fileName}");
            }

            // Copy lobby_connect tools for LAN multiplayer
            string[] lobbyConnectPaths =
            [
                Path.Combine(tempDir, "release", "tools", "lobby_connect", "lobby_connect_x64.exe"),
                Path.Combine(tempDir, "release", "tools", "lobby_connect", "lobby_connect_x32.exe"),
                Path.Combine(tempDir, "tools", "lobby_connect", "lobby_connect_x64.exe"),
                Path.Combine(tempDir, "tools", "lobby_connect", "lobby_connect_x32.exe")
            ];

            foreach (string lobbyPath in lobbyConnectPaths.Where(File.Exists))
            {
                string fileName = Path.GetFileName(lobbyPath);
                string destPath = Path.Combine(BinPath, fileName);
                File.Copy(lobbyPath, destPath, true);
                Debug.WriteLine($"Copied lobby_connect tool to _bin: {fileName}");
            }

            // Clean up
            File.Delete(tempFile);
            Directory.Delete(tempDir, true);

            // Update version file
            await File.WriteAllTextAsync(versionFile, latestVersion);
        }
        catch (Exception ex)
        {
            // Silently ignore - not critical, probably rate limited
            LogHelper.LogError("UpdateGoldBerg failed", ex);
        }
    }

    /// <summary>
    ///     Extracts an archive using 7-Zip.
    /// </summary>
    private static async Task ExtractFileAsync(string sourceArchive, string destination)
    {
        try
        {
            string sevenZipPath = Path.Combine(BinPath, "7z", "7za.exe");
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = sevenZipPath,
                Arguments = $"x \"{sourceArchive}\" -y -o\"{destination}\""
            };

            using var process = Process.Start(startInfo);
            if (process is not null && !process.HasExited)
            {
                await Task.Run(() => process.WaitForExit());
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"ExtractFileAsync failed for {sourceArchive}", ex);
            MessageBox.Show(
                "Unable to extract updated files. If you have WinRAR, try uninstalling it then trying again!",
                "Extraction Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    /// <summary>
    ///     Checks for internet connectivity by pinging a single host.
    /// </summary>
    private static async Task<bool> CheckForInternetAsync(string host)
    {
        using var myPing = new Ping();
        byte[] buffer = new byte[32];
        const int timeout = 20000; // 20 seconds - async so no UI blocking
        var pingOptions = new PingOptions();

        try
        {
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

    /// <summary>
    ///     Copies a file if the source exists.
    /// </summary>
    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Copy(source, destination, true);
        }
    }
}
