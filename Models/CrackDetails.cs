namespace APPID.Models;

/// <summary>
///     Tracks details about what was cracked during a crack operation
/// </summary>
public class CrackDetails
{
    public string GameName { get; set; }
    public string GamePath { get; set; }
    public string AppId { get; set; }
    public List<string> DllsBackedUp { get; } = [];
    public List<string> DllsReplaced { get; } = [];
    public List<string> ExesTried { get; } = []; // All EXEs Steamless attempted
    public List<string> ExesUnpacked { get; } = []; // EXEs with Steam Stub that were unpacked
    public List<string> ExesSkipped { get; } = []; // Legacy - no longer used
    public List<string> Errors { get; } = [];
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Zip/Upload tracking
    public bool ZipAttempted { get; set; }
    public bool ZipSuccess { get; set; }
    public string ZipError { get; set; }
    public string ZipPath { get; set; }
    public TimeSpan? ZipDuration { get; set; }
    public long ZipFileSize { get; set; }
    public bool UploadAttempted { get; set; }
    public bool UploadSuccess { get; set; }
    public string UploadError { get; set; }
    public string UploadUrl { get; set; } // 1fichier URL
    public string PyDriveUrl { get; set; } // Converted PyDrive URL
    public int UploadRetryCount { get; set; }
    public TimeSpan? UploadDuration { get; set; }

    public bool HasAnyChanges => DllsReplaced.Count > 0 || ExesUnpacked.Count > 0;
    public bool HasDetails => HasAnyChanges || ZipAttempted || UploadAttempted;

    public string GetSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Crack Details for {GameName} ===");
        sb.AppendLine($"Path: {GamePath}");
        sb.AppendLine($"AppID: {AppId}");
        sb.AppendLine($"Time: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Success: {Success}");
        sb.AppendLine();

        if (DllsBackedUp.Count > 0)
        {
            sb.AppendLine($"DLLs Backed Up ({DllsBackedUp.Count}):");
            foreach (var dll in DllsBackedUp)
            {
                sb.AppendLine($"  - {dll}");
            }

            sb.AppendLine();
        }

        if (DllsReplaced.Count > 0)
        {
            sb.AppendLine($"DLLs Replaced ({DllsReplaced.Count}):");
            foreach (var dll in DllsReplaced)
            {
                sb.AppendLine($"  - {dll}");
            }

            sb.AppendLine();
        }

        if (ExesTried.Count > 0)
        {
            sb.AppendLine($"EXEs Scanned by Steamless ({ExesTried.Count}):");
            foreach (var exe in ExesTried)
            {
                bool wasUnpacked = ExesUnpacked.Any(u => u.EndsWith(exe));
                sb.AppendLine($"  - {exe} {(wasUnpacked ? "[UNPACKED - Had Steam Stub]" : "[No Steam Stub]")}");
            }

            sb.AppendLine();
        }

        if (Errors.Count > 0)
        {
            sb.AppendLine($"Errors ({Errors.Count}):");
            foreach (var err in Errors)
            {
                sb.AppendLine($"  - {err}");
            }

            sb.AppendLine();
        }

        // Zip status
        if (ZipAttempted)
        {
            sb.AppendLine($"Zip: {(ZipSuccess ? "Success" : "Failed")}");
            if (!string.IsNullOrEmpty(ZipPath))
            {
                sb.AppendLine($"  Path: {ZipPath}");
            }

            if (!string.IsNullOrEmpty(ZipError))
            {
                sb.AppendLine($"  Error: {ZipError}");
            }

            sb.AppendLine();
        }

        // Upload status
        if (UploadAttempted)
        {
            sb.AppendLine($"Upload: {(UploadSuccess ? "Success" : "Failed")}");
            if (UploadRetryCount > 0)
            {
                sb.AppendLine($"  Retries: {UploadRetryCount}");
            }

            if (!string.IsNullOrEmpty(UploadUrl))
            {
                sb.AppendLine($"  URL: {UploadUrl}");
            }

            if (!string.IsNullOrEmpty(UploadError))
            {
                sb.AppendLine($"  Error: {UploadError}");
            }
        }

        return sb.ToString();
    }
}
