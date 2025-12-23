using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
/// Implementation of Steamless EXE unpacking service.
/// </summary>
public sealed class SteamlessService : ISteamlessService
{
    private readonly string _binPath;

    public SteamlessService(string binPath)
    {
        _binPath = binPath ?? throw new ArgumentNullException(nameof(binPath));
    }

    public async Task<SteamlessResult> UnpackExeAsync(string exePath, string workingDirectory, 
        Action<string>? statusCallback = null)
    {
        try
        {
            string steamlessPath = Path.Combine(_binPath, "Steamless", "Steamless.CLI.exe");
            
            if (!File.Exists(steamlessPath))
            {
                LogHelper.Log($"[STEAMLESS] ERROR: Steamless.CLI.exe not found at {steamlessPath}");
                return new SteamlessResult
                {
                    Success = false,
                    UnpackedFileCreated = false,
                    ErrorMessage = "Steamless.CLI.exe not found"
                };
            }

            // Restore .bak if it exists (apply Steamless to clean exe)
            if (File.Exists($"{exePath}.bak"))
            {
                File.Delete(exePath);
                File.Move($"{exePath}.bak", exePath);
            }

            statusCallback?.Invoke($"Processing {Path.GetFileName(exePath)} with Steamless...");
            LogHelper.Log($"[STEAMLESS] Processing: {exePath}");

            var processInfo = new ProcessStartInfo
            {
                FileName = steamlessPath,
                Arguments = $"\"{exePath}\"",
                WorkingDirectory = workingDirectory,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return new SteamlessResult
                {
                    Success = false,
                    UnpackedFileCreated = false,
                    ErrorMessage = "Failed to start Steamless process"
                };
            }

            // Read output asynchronously
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await Task.Run(() => process.WaitForExit());
            
            string output = await outputTask + await errorTask;
            LogHelper.Log($"[STEAMLESS] Exit code: {process.ExitCode}, Output: {output.Trim()}");

            // Check if unpacked file was created
            string unpackedPath = $"{exePath}.unpacked.exe";
            bool unpackedExists = File.Exists(unpackedPath);

            if (unpackedExists)
            {
                // Replace original with unpacked version
                File.Move(exePath, $"{exePath}.bak");
                File.Move(unpackedPath, exePath);
                
                LogHelper.Log($"[STEAMLESS] SUCCESS - Unpacked: {Path.GetFileName(exePath)}");
                
                return new SteamlessResult
                {
                    Success = true,
                    UnpackedFileCreated = true,
                    OutputPath = exePath
                };
            }
            else
            {
                LogHelper.Log($"[STEAMLESS] No stub detected: {Path.GetFileName(exePath)}");
                return new SteamlessResult
                {
                    Success = true,
                    UnpackedFileCreated = false,
                    ErrorMessage = "No stub detected in EXE"
                };
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Steamless unpacking failed for {exePath}", ex);
            return new SteamlessResult
            {
                Success = false,
                UnpackedFileCreated = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
