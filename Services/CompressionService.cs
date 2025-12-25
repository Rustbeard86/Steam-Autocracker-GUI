using System.IO.Compression;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of compression service using 7-Zip or built-in .NET compression.
/// </summary>
public sealed class CompressionService(string binPath) : ICompressionService
{
    private readonly string _binPath = binPath ?? throw new ArgumentNullException(nameof(binPath));

    public async Task<bool> CompressAsync(string sourcePath, string outputPath, string format,
        string level, bool usePassword, Action<int>? progressCallback = null)
    {
        try
        {
            // Try to find 7-Zip
            string sevenZipPath = FindSevenZip();

            if (sevenZipPath is null)
            {
                // Fall back to built-in .NET compression for basic zip without password
                if (format.Equals("zip", StringComparison.OrdinalIgnoreCase) && !usePassword)
                {
                    await Task.Run(() =>
                    {
                        ZipFile.CreateFromDirectory(
                            sourcePath,
                            outputPath,
                            CompressionLevel.Optimal,
                            false);
                    });
                    progressCallback?.Invoke(100);
                    return true;
                }

                LogHelper.LogError("7-Zip not found and .NET fallback not applicable", null);
                return false;
            }

            // Build 7-zip arguments
            string compressionSwitch = GetCompressionSwitch(level);
            string archiveType = format.Equals("7z", StringComparison.OrdinalIgnoreCase) ? "7z" : "zip";
            string passwordArg = usePassword ? "-p\"cs.rin.ru\" -mhe=on" : "";
            string arguments =
                $"a -t{archiveType} {compressionSwitch} {passwordArg} -bsp1 \"{outputPath}\" \"{sourcePath}\\*\" -r";

            // Execute 7-zip with progress tracking
            var processInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return false;
            }

            // Monitor progress from output
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var match = Regex.Match(e.Data, @"(\d+)%");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int percentage))
                    {
                        progressCallback?.Invoke(percentage);
                    }
                }
            };

            process.BeginOutputReadLine();
            await Task.Run(() => process.WaitForExit());

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Compression failed", ex);
            return false;
        }
    }

    private static string? FindSevenZip()
    {
        string[] possiblePaths =
        [
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        ];

        return possiblePaths.FirstOrDefault(File.Exists);
    }

    private static string GetCompressionSwitch(string level) => level.ToLower() switch
    {
        "no compression" => "-mx0",
        "fast" => "-mx1",
        "normal" => "-mx5",
        "maximum" => "-mx9",
        "ultra" => "-mx9 -mfb=273 -ms=on",
        _ => "-mx5" // Default to normal
    };
}
