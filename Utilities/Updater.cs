using System.Net.NetworkInformation;
using System.Text.Json;
using APPID.Utilities.Network;

namespace APPID.Utilities;

/// <summary>
///     Handles checking for and downloading updates for external dependencies like Steamless and Goldberg emulator.
/// </summary>
internal static class Updater
{
    private static readonly string[] DnsServers = ["1.1.1.1", "8.8.8.8", "208.67.222.222"];

    private static readonly string BinPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory,
        "_bin");

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
    ///     Checks for newer version of Steamless on GitHub and updates if available.
    /// </summary>
    public static async Task CheckGitHubNewerVersion(string user, string repo)
    {
        string url = $"https://api.github.com/repos/{user}/{repo}/releases";
        try
        {
            // Use Insecure client singleton for SSL bypass + User-Agent already configured
            var client = HttpClientFactory.Insecure;

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var releases = doc.RootElement;
            if (releases.GetArrayLength() == 0)
            {
                return;
            }

            var latestRelease = releases[0];
            string latestGitHubVersion = latestRelease.GetProperty("tag_name").GetString()?.Replace("v", "") ?? "";
            string versionFilePath = Path.Combine(BinPath, $"{repo}.ver");

            string localVersion = File.Exists(versionFilePath)
                ? await File.ReadAllTextAsync(versionFilePath)
                : string.Empty;

            localVersion = localVersion.Trim();

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

            // Find and download the zip asset
            if (latestRelease.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        string downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            continue;
                        }

                        // Download the new version
                        var downloadResponse = await client.GetAsync(downloadUrl);
                        await using (FileStream fs = new("SLS.zip", FileMode.Create))
                        {
                            await downloadResponse.Content.CopyToAsync(fs);
                        }

                        // Extract and update
                        await ExtractFileAsync("SLS.zip", steamlessPath);
                        File.Delete("SLS.zip");
                        await File.WriteAllTextAsync(versionFilePath, latestGitHubVersion);
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogNetwork($"GitHub API check skipped: {ex.Message}");
        }
    }

    /// <summary>
    ///     Checks for and downloads updates for Goldberg Steam emulator.
    /// </summary>
    public static async Task UpdateGoldBergAsync()
    {
        const string url = "https://api.github.com/repos/Detanup01/gbe_fork/releases/latest";
        try
        {
            // Use Insecure client singleton for SSL bypass + User-Agent already configured
            var client = HttpClientFactory.Insecure;

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(@"Unable to check for Goldberg updates. Please try again later.");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var release = doc.RootElement;

            string latestVersion = release.GetProperty("tag_name").GetString()?.Replace("release-", "") ?? "";
            string versionFile = Path.Combine(BinPath, "Goldberg", "version.txt");

            string localVersion = File.Exists(versionFile)
                ? await File.ReadAllTextAsync(versionFile)
                : string.Empty;

            localVersion = localVersion.Trim();

            if (localVersion == latestVersion)
            {
                return; // Already up to date
            }

            LogHelper.LogUpdate("Goldberg (gbe_fork)", $"{localVersion} -> {latestVersion}");

            // Find the Windows release asset
            string? downloadUrl = null;
            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName == "emu-win-release.7z")
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show(@"Windows release asset not found for Goldberg fork.");
                return;
            }

            // Download the new version
            const string tempFile = "gbe_fork.7z";
            var downloadResponse = await client.GetAsync(downloadUrl);
            await using (FileStream fs = new(tempFile, FileMode.Create))
            {
                await downloadResponse.Content.CopyToAsync(fs);
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

            CopyIfExists(
                Path.Combine(tempDir, "release", "regular", "x64", "steam_api64.dll"),
                Path.Combine(goldbergDir, "steam_api64.dll"));

            CopyIfExists(
                Path.Combine(tempDir, "release", "regular", "x32", "steam_api.dll"),
                Path.Combine(goldbergDir, "steam_api.dll"));

            // Copy generate_interfaces tools
            string[] toolPaths =
            [
                Path.Combine(tempDir, "release", "tools", "generate_interfaces", "generate_interfaces_x64.exe"),
                Path.Combine(tempDir, "release", "tools", "generate_interfaces", "generate_interfaces_x32.exe")
            ];

            foreach (string toolPath in toolPaths.Where(File.Exists))
            {
                string destPath = Path.Combine(goldbergDir, Path.GetFileName(toolPath));
                File.Copy(toolPath, destPath, true);
            }

            // Copy lobby_connect tools
            string[] lobbyPaths =
            [
                Path.Combine(tempDir, "release", "tools", "lobby_connect", "lobby_connect_x64.exe"),
                Path.Combine(tempDir, "release", "tools", "lobby_connect", "lobby_connect_x32.exe")
            ];

            foreach (string lobbyPath in lobbyPaths.Where(File.Exists))
            {
                string destPath = Path.Combine(BinPath, Path.GetFileName(lobbyPath));
                File.Copy(lobbyPath, destPath, true);
            }

            // Clean up
            File.Delete(tempFile);
            Directory.Delete(tempDir, true);

            // Update version file
            await File.WriteAllTextAsync(versionFile, latestVersion);
        }
        catch (Exception ex)
        {
            LogHelper.LogError("UpdateGoldberg failed", ex);
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
                Arguments = $"x \"{sourceArchive}\" -y -o\"{destination}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"ExtractFileAsync failed for {sourceArchive}", ex);
            MessageBox.Show(
                @"Unable to extract updated files. If you have WinRAR, try uninstalling it then trying again!",
                @"Extraction Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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
            const int timeout = 20000; // 20 seconds - async so no UI blocking
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
