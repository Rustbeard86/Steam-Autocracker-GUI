using System.Text.Json;
using APPID.Utilities.Network;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace APPID.Services;

/// <summary>
///     Handles downloading and setup of external dependencies (7-Zip, Goldberg emulator).
/// </summary>
internal static class DependencySetupService
{
    private static readonly string BinPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory,
        "_bin");

    /// <summary>
    ///     Ensures all required dependencies are available. Downloads missing ones.
    ///     7-Zip is installed first, then used for other extractions.
    /// </summary>
    public static async Task EnsureDependenciesAsync(Action<string>? statusCallback = null)
    {
        try
        {
            // CRITICAL: Install 7-Zip FIRST - needed for Goldberg extraction
            await Ensure7ZipAsync(statusCallback);

            // Now that 7-Zip is available, check Goldberg
            await EnsureGoldbergAsync(statusCallback);

            // All done - reset status after short delay
            await Task.Delay(2000);
            statusCallback?.Invoke("Click folder & select game's parent directory.");
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Dependency setup failed", ex);
            statusCallback?.Invoke($"Warning: Some dependencies failed to download - {ex.Message}");
        }
    }

    /// <summary>
    ///     Downloads and extracts 7-Zip extra package from official GitHub releases.
    ///     Uses SharpCompress for bootstrap extraction (no external tools needed).
    /// </summary>
    private static async Task Ensure7ZipAsync(Action<string>? statusCallback)
    {
        string sevenZipExe = Path.Combine(BinPath, "7z", "7za.exe");

        // Check if 7-Zip exists AND is actually usable (not a broken SFX installer)
        if (File.Exists(sevenZipExe))
        {
            // Quick validation - try to run it with --help
            try
            {
                var testPsi = new ProcessStartInfo
                {
                    FileName = sevenZipExe,
                    Arguments = "--help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var testProcess = Process.Start(testPsi);
                if (testProcess != null)
                {
                    await testProcess.WaitForExitAsync();

                    // If it ran successfully without elevation error, it's good
                    if (testProcess.ExitCode == 0 || testProcess.ExitCode == 1) // 0 or 1 are both valid for --help
                    {
                        return; // Working 7-Zip found
                    }
                }
            }
            catch (Exception ex)
            {
                // If validation fails (elevation error, etc.), delete and re-download
                LogHelper.Log($"[SETUP] Existing 7za.exe failed validation: {ex.Message}");
                try
                {
                    File.Delete(sevenZipExe);
                    // Also delete the directory to clean up
                    string sevenZipDir = Path.GetDirectoryName(sevenZipExe)!;
                    if (Directory.Exists(sevenZipDir))
                    {
                        Directory.Delete(sevenZipDir, true);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        statusCallback?.Invoke("Downloading 7-Zip (one-time setup)...");
        LogHelper.Log("[SETUP] 7-Zip not found or invalid, downloading from GitHub...");

        try
        {
            var client = HttpClientFactory.Insecure;
            const string apiUrl = "https://api.github.com/repos/ip7z/7zip/releases/latest";

            var response = await client.GetAsync(apiUrl);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to fetch 7-Zip release info: {response.StatusCode}");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var release = doc.RootElement;

            // Find 7z-extra.7z asset
            string? downloadUrl = null;
            if (release.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString() ?? "";
                    if (assetName.Contains("-extra.7z", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new Exception("7-Zip extra package not found in latest release");
            }

            // Download 7z-extra.7z
            string tempArchive = Path.Combine(Path.GetTempPath(), "7z-extra.7z");
            statusCallback?.Invoke("Downloading 7-Zip...");

            var downloadResponse = await client.GetAsync(downloadUrl);
            await using (var fs = new FileStream(tempArchive, FileMode.Create))
            {
                await downloadResponse.Content.CopyToAsync(fs);
            }

            // Extract using SharpCompress (no external tools needed!)
            string sevenZipDir = Path.Combine(BinPath, "7z");
            Directory.CreateDirectory(sevenZipDir);

            statusCallback?.Invoke("Extracting 7-Zip...");
            await ExtractWithSharpCompressAsync(tempArchive, sevenZipDir);

            // Clean up
            try { File.Delete(tempArchive); }
            catch
            {
                // ignored
            }

            // Verify extraction - check for x64 version first, fallback to root
            string x64Exe = Path.Combine(sevenZipDir, "x64", "7za.exe");
            if (File.Exists(x64Exe))
            {
                // Copy x64 version to root for easy access
                File.Copy(x64Exe, sevenZipExe, true);

                // Also copy DLLs
                string x64Dll = Path.Combine(sevenZipDir, "x64", "7za.dll");
                if (File.Exists(x64Dll))
                {
                    File.Copy(x64Dll, Path.Combine(sevenZipDir, "7za.dll"), true);
                }
            }

            if (File.Exists(sevenZipExe))
            {
                LogHelper.Log("[SETUP] 7-Zip installed successfully");
                statusCallback?.Invoke("7-Zip ready");
            }
            else
            {
                throw new Exception("7-Zip extraction failed - executable not found");
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError("7-Zip setup failed", ex);
            throw;
        }
    }

    /// <summary>
    ///     Extracts an archive using SharpCompress library (supports .7z, .zip, .tar, etc.).
    /// </summary>
    private static async Task ExtractWithSharpCompressAsync(string archivePath, string destinationPath)
    {
        await Task.Run(() =>
        {
            using var archive = ArchiveFactory.Open(archivePath);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                entry.WriteToDirectory(destinationPath,
                    new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
            }
        });
    }

    /// <summary>
    ///     Downloads and extracts Goldberg emulator, checking for updates.
    /// </summary>
    private static async Task EnsureGoldbergAsync(Action<string>? statusCallback)
    {
        const string apiUrl = "https://api.github.com/repos/Detanup01/gbe_fork/releases/latest";

        try
        {
            var client = HttpClientFactory.Insecure;
            var response = await client.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                LogHelper.LogNetwork($"Failed to check Goldberg updates: {response.StatusCode}");
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var release = doc.RootElement;

            string latestVersion = release.GetProperty("tag_name").GetString()?.Replace("release-", "") ?? "";
            string versionFile = Path.Combine(BinPath, "Goldberg", "version.txt");

            string localVersion = File.Exists(versionFile)
                ? (await File.ReadAllTextAsync(versionFile)).Trim()
                : string.Empty;

            if (localVersion == latestVersion)
            {
                return; // Already up to date
            }

            statusCallback?.Invoke($"Updating Goldberg emulator ({localVersion} → {latestVersion})...");
            LogHelper.LogUpdate("Goldberg (gbe_fork)", $"{localVersion} → {latestVersion}");

            // Find download URL
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
                throw new Exception("Goldberg release asset not found");
            }

            // Download archive
            const string tempFile = "gbe_fork.7z";
            var downloadResponse = await client.GetAsync(downloadUrl);
            await using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await downloadResponse.Content.CopyToAsync(fs);
            }

            // Extract using 7-Zip
            string tempDir = Path.Combine(BinPath, "temp_goldberg");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            await Extract7zArchiveAsync(tempFile, tempDir);

            // Copy all files from release/ to Goldberg/
            string goldbergDir = Path.Combine(BinPath, "Goldberg");
            string releaseDir = Path.Combine(tempDir, "release");

            if (Directory.Exists(goldbergDir))
            {
                Directory.Delete(goldbergDir, true);
            }

            Directory.CreateDirectory(goldbergDir);
            CopyDirectory(releaseDir, goldbergDir);

            // DON'T copy lobby_connect to _bin root - consume from Goldberg/tools/lobby_connect directly
            LogHelper.Log("[SETUP] Goldberg extracted to _bin/Goldberg (tools remain in subdirectories)");

            // Clean up
            try { File.Delete(tempFile); }
            catch
            {
                // ignored
            }

            try { Directory.Delete(tempDir, true); }
            catch
            {
                // ignored
            }

            // Update version file
            await File.WriteAllTextAsync(versionFile, latestVersion);

            LogHelper.Log("[SETUP] Goldberg emulator updated successfully");
            statusCallback?.Invoke("Goldberg emulator ready");
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Goldberg setup failed", ex);
            // Don't throw - allow app to continue with old version
        }
    }

    /// <summary>
    ///     Extracts a 7z archive using 7za.exe.
    /// </summary>
    private static async Task Extract7zArchiveAsync(string archivePath, string destinationPath)
    {
        string sevenZipPath = Path.Combine(BinPath, "7z", "7za.exe");

        if (!File.Exists(sevenZipPath))
        {
            throw new FileNotFoundException("7za.exe not found - cannot extract archive");
        }

        var psi = new ProcessStartInfo
        {
            FileName = sevenZipPath,
            Arguments = $"x \"{archivePath}\" -y -o\"{destinationPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"7-Zip extraction failed (code {process.ExitCode}): {error}");
            }
        }
    }

    /// <summary>
    ///     Recursively copies a directory.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir);
        }
    }
}
