using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
/// Implementation of Steam game cracking service using emulator DLLs.
/// </summary>
public sealed class CrackingService(string binPath) : ICrackingService
{
    private readonly string _binPath = binPath ?? throw new ArgumentNullException(nameof(binPath));

    public async Task<CrackResult> CrackGameAsync(string gameDir, string appId, bool useGoldberg, 
        Action<string>? statusCallback = null)
    {
        var result = new CrackResult();
        
        try
        {
            LogHelper.Log($"[CRACK] Starting crack for game in: {gameDir} (AppID: {appId})");
            statusCallback?.Invoke($"Scanning game directory...");

            var files = Directory.GetFiles(gameDir, "*.*", SearchOption.AllDirectories);
            LogHelper.Log($"[CRACK] Found {files.Length} files, scanning for Steam DLLs...");

            foreach (string file in files)
            {
                // Process steam_api64.dll
                if (file.EndsWith("steam_api64.dll", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessSteamDllAsync(file, "steam_api64.dll", appId, useGoldberg, statusCallback, result);
                }
                // Process steam_api.dll
                else if (file.EndsWith("steam_api.dll", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessSteamDllAsync(file, "steam_api.dll", appId, useGoldberg, statusCallback, result);
                }
            }

            result = result with { Success = result.DllsReplaced.Count > 0 };
            
            if (result.Success)
            {
                LogHelper.Log($"[CRACK] SUCCESS - Replaced {result.DllsReplaced.Count} DLL(s)");
            }
            else
            {
                LogHelper.Log("[CRACK] No Steam DLLs found to replace");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogHelper.LogError("Cracking failed", ex);
            return result with { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task ProcessSteamDllAsync(string dllPath, string dllName, string appId, 
        bool useGoldberg, Action<string>? statusCallback, CrackResult result)
    {
        try
        {
            string emulatorName = useGoldberg ? "Goldberg" : "ALI213";
            string sourceDll = Path.Combine(_binPath, emulatorName, dllName);

            if (!File.Exists(sourceDll))
            {
                LogHelper.Log($"[CRACK] ERROR: Emulator DLL not found: {sourceDll}");
                return;
            }

            statusCallback?.Invoke($"Processing {dllName} with {emulatorName}...");

            // Restore .bak if it exists (get clean file first)
            if (File.Exists($"{dllPath}.bak"))
            {
                File.Delete(dllPath);
                File.Move($"{dllPath}.bak", dllPath);
            }

            // Check if already using emulator DLL
            if (AreFilesIdentical(dllPath, sourceDll))
            {
                LogHelper.Log($"[CRACK] {dllName} is already {emulatorName}, skipping");
                result.DllsReplaced.Add($"{dllPath} (already {emulatorName})");
                return;
            }

            // Backup and replace
            File.Move(dllPath, $"{dllPath}.bak");
            result.DllsBackedUp.Add(dllPath);

            File.Copy(sourceDll, dllPath);
            result.DllsReplaced.Add($"{dllPath} ({emulatorName})");

            // Create steam_settings for Goldberg
            if (useGoldberg)
            {
                string parentDir = Path.GetDirectoryName(dllPath)!;
                string steamSettings = Path.Combine(parentDir, "steam_settings");
                
                if (Directory.Exists(steamSettings))
                {
                    Directory.Delete(steamSettings, true);
                }
                Directory.CreateDirectory(steamSettings);
                
                // Copy steam_appid.txt
                string steamAppIdFile = Path.Combine(steamSettings, "steam_appid.txt");
                await File.WriteAllTextAsync(steamAppIdFile, appId);
            }
            // Handle ALI213 configuration
            else
            {
                string parentDir = Path.GetDirectoryName(dllPath)!;
                string configPath = Path.Combine(parentDir, "SteamConfig.ini");
                
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }
                
                // Copy and configure ALI213 ini
                string sourceIni = Path.Combine(_binPath, "ALI213", "SteamConfig.ini");
                if (File.Exists(sourceIni))
                {
                    File.Copy(sourceIni, configPath);
                    // Update AppID in the ini file
                    string iniContent = await File.ReadAllTextAsync(configPath);
                    iniContent = iniContent.Replace("AppID = 0", $"AppID = {appId}");
                    await File.WriteAllTextAsync(configPath, iniContent);
                }
            }

            LogHelper.Log($"[CRACK] Successfully replaced {dllName} with {emulatorName}");
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Failed to process {dllName}", ex);
        }
    }

    public bool AreFilesIdentical(string file1, string file2)
    {
        try
        {
            if (!File.Exists(file1) || !File.Exists(file2)) return false;

            var info1 = new FileInfo(file1);
            var info2 = new FileInfo(file2);

            // Quick check: if sizes differ, files are different
            if (info1.Length != info2.Length) return false;

            // For small files (< 10KB), do full comparison
            // For larger files, compare first 8KB as a reasonable heuristic for DLL headers
            const int comparisonSize = 8192; // 8KB
            int bytesToCompare = (int)Math.Min(info1.Length, comparisonSize);

            using var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read);
            using var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read);
            
            byte[] buffer1 = new byte[bytesToCompare];
            byte[] buffer2 = new byte[bytesToCompare];
            
            int read1 = fs1.Read(buffer1, 0, bytesToCompare);
            int read2 = fs2.Read(buffer2, 0, bytesToCompare);
            
            if (read1 != read2) return false;
            
            return buffer1.SequenceEqual(buffer2);
        }
        catch
        {
            return false;
        }
    }
}
