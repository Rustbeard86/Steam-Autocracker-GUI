using APPID.Services.Interfaces;
using Steamless.API.Events;
using Steamless.API.Services;
using Steamless.Library;

namespace APPID.Services;

/// <summary>
///     Implementation of Steamless EXE unpacking service using Steamless.Library.
/// </summary>
public sealed class SteamlessService : ISteamlessService
{
    public async Task<SteamlessResult> UnpackExeAsync(string exePath, string workingDirectory,
        Action<string>? statusCallback = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(exePath))
                {
                    LogHelper.Log($"[STEAMLESS] ERROR: File not found at {exePath}");
                    return new SteamlessResult
                    {
                        Success = false, UnpackedFileCreated = false, ErrorMessage = "EXE file not found"
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

                // Create logging service to capture Steamless output
                var logService = new LoggingService();
                bool hasStubDetection = false;
                bool hasError = false;

                logService.AddLogMessage += (_, e) =>
                {
                    string message = $"[STEAMLESS-LIB] [{e.MessageType}] {e.Message}";
                    LogHelper.Log(message);

                    // Track if stub was detected (indicates EXE has Steam protection)
                    if (e.Message.Contains("SteamStub", StringComparison.OrdinalIgnoreCase) ||
                        e.Message.Contains("variant", StringComparison.OrdinalIgnoreCase) ||
                        e.Message.Contains("detected", StringComparison.OrdinalIgnoreCase))
                    {
                        hasStubDetection = true;
                    }

                    // Track errors
                    if (e.MessageType == LogMessageType.Error)
                    {
                        hasError = true;
                    }

                    // Forward status updates to callback
                    if (e.MessageType != LogMessageType.Debug)
                    {
                        statusCallback?.Invoke(e.Message);
                    }
                };

                // Create the unpacker instance
                var unpacker = new SteamlessLibrary();

                // Process the file
                bool success = unpacker.Unpack(exePath);

                // Check if unpacked file was created
                string unpackedPath = $"{exePath}.unpacked.exe";
                bool unpackedExists = File.Exists(unpackedPath);

                if (success && unpackedExists)
                {
                    // Replace original with unpacked version
                    File.Move(exePath, $"{exePath}.bak");
                    File.Move(unpackedPath, exePath);

                    LogHelper.Log($"[STEAMLESS] SUCCESS - Unpacked: {Path.GetFileName(exePath)}");

                    return new SteamlessResult { Success = true, UnpackedFileCreated = true, OutputPath = exePath };
                }

                // Success=true but no file means no stub detected
                if (success && !unpackedExists)
                {
                    LogHelper.Log($"[STEAMLESS] No stub detected: {Path.GetFileName(exePath)}");
                    return new SteamlessResult
                    {
                        Success = true, UnpackedFileCreated = false, ErrorMessage = "No stub detected in EXE"
                    };
                }

                // Failed to process - but check if stub was detected first
                if (hasStubDetection && !unpackedExists)
                {
                    // Stub was detected but unpacking failed - this is a real error
                    string errorMsg = "Steam Stub detected but unpacking failed - see log for details";
                    LogHelper.Log($"[STEAMLESS] FAILED: {errorMsg}");
                    return new SteamlessResult
                    {
                        Success = false, UnpackedFileCreated = false, ErrorMessage = errorMsg
                    };
                }

                // No stub detected and processing failed - not really an error
                string noStubMsg = hasError ? "Unpacking failed - see log for details" : "No Steam Stub found in EXE";
                LogHelper.Log($"[STEAMLESS] {noStubMsg}");

                return new SteamlessResult
                {
                    Success = true, // Not an error if there's just no stub
                    UnpackedFileCreated = false,
                    ErrorMessage = noStubMsg
                };
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"Steamless unpacking failed for {exePath}", ex);
                return new SteamlessResult { Success = false, UnpackedFileCreated = false, ErrorMessage = ex.Message };
            }
        });
    }
}
