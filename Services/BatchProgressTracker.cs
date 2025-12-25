using APPID.Models;

namespace APPID.Services;

/// <summary>
///     Tracks batch processing progress with time-based estimation and live adjustment
/// </summary>
public sealed class BatchProgressTracker
{
    private const double ConversionTimePerFile = 45.0;
    private const double RetryBuffer = 1.3;

    // Folder sizes for estimation
    private readonly Dictionary<string, long> _folderSizes;
    private readonly List<BatchGameItem> _games;
    private readonly BatchProcessingSettings _settings;
    private readonly DateTime _startTime;
    private readonly long _totalBytesToUpload;
    private readonly long _totalBytesToZip;
    private long _bytesUploadedSoFar;
    private long _bytesZippedSoFar;

    // Track completed work
    private int _cracksCompleted;
    private int _lastPercent;
    private double _uploadRate;

    // Persisted rates from previous sessions (loaded from settings)
    private double _zipRate;

    public BatchProgressTracker(List<BatchGameItem> games, BatchProcessingSettings settings)
    {
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startTime = DateTime.Now;

        // Load persisted rates from settings, fall back to defaults
        _zipRate = settings.CompressionLevel == "0" ? 50_000_000.0 : 30_000_000.0;
        _uploadRate = 5_000_000.0; // 5 MB/s default

        // Calculate folder sizes
        _folderSizes = new Dictionary<string, long>();
        foreach (var game in games)
        {
            try
            {
                long size = Directory.GetFiles(game.Path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
                _folderSizes[game.Path] = size;

                if (game.ShouldZip)
                {
                    _totalBytesToZip += size;
                }

                if (game.ShouldUpload)
                {
                    _totalBytesToUpload += size;
                }
            }
            catch
            {
                _folderSizes[game.Path] = 1_000_000_000; // 1GB fallback
            }
        }
    }

    public BatchProgress UpdateForCrackComplete()
    {
        _cracksCompleted++;
        return CalculateProgress("Cracking");
    }

    public BatchProgress UpdateForZipProgress(string gamePath, int percentage, double measuredSpeed = 0)
    {
        if (measuredSpeed > 0)
        {
            _zipRate = measuredSpeed; // Update with actual measured speed
        }

        // Estimate bytes zipped for this game
        long gameSize = _folderSizes.GetValueOrDefault(gamePath, 0);
        _bytesZippedSoFar = _cracksCompleted * (gameSize / 100) + (long)(gameSize * percentage / 100.0);

        return CalculateProgress("Compressing", percentage);
    }

    public BatchProgress UpdateForUploadProgress(string gamePath, long bytesTransferred, long totalBytes, double speed)
    {
        if (speed > 0)
        {
            _uploadRate = speed; // Update with actual upload speed
        }

        _bytesUploadedSoFar += bytesTransferred;

        int percentage = totalBytes > 0 ? (int)(bytesTransferred * 100 / totalBytes) : 0;
        return CalculateProgress("Uploading", percentage);
    }

    public BatchProgress UpdateForConversionProgress(string gamePath)
    {
        return CalculateProgress("Converting");
    }

    private BatchProgress CalculateProgress(string phase, int phaseProgress = 0)
    {
        // Recalculate based on actual speeds
        double estCrackTime = (_games.Count(g => g.ShouldCrack) - _cracksCompleted) * 3.0;
        double remainingZipTime = (_totalBytesToZip - _bytesZippedSoFar) / _zipRate;
        double remainingUploadTime = (_totalBytesToUpload - _bytesUploadedSoFar) / _uploadRate;
        double remainingConversionTime = _games.Count(g => g.ShouldUpload) * ConversionTimePerFile *
                                         (1.0 - (_bytesUploadedSoFar / (double)Math.Max(1, _totalBytesToUpload)));

        double totalRemaining = estCrackTime + remainingZipTime + remainingUploadTime + remainingConversionTime;
        double elapsed = (DateTime.Now - _startTime).TotalSeconds;
        double estTotalTime = elapsed + totalRemaining;

        int percent = estTotalTime > 0 ? (int)(elapsed / estTotalTime * 100) : 0;
        percent = Math.Max(_lastPercent, Math.Min(99, percent)); // Never decrease, cap at 99
        _lastPercent = percent;

        return new BatchProgress
        {
            Phase = phase,
            OverallPercentage = percent,
            PhasePercentage = phaseProgress,
            EstimatedSecondsRemaining = totalRemaining,
            CrackedCount = _cracksCompleted,
            ZippedCount =
                (int)(_bytesZippedSoFar / (double)Math.Max(1, _totalBytesToZip) * _games.Count(g => g.ShouldZip)),
            UploadedCount = (int)(_bytesUploadedSoFar / (double)Math.Max(1, _totalBytesToUpload) *
                                  _games.Count(g => g.ShouldUpload))
        };
    }

    public BatchProgress GetFinalProgress()
    {
        _lastPercent = 100;
        return new BatchProgress
        {
            Phase = "Complete",
            OverallPercentage = 100,
            EstimatedSecondsRemaining = 0,
            CrackedCount = _cracksCompleted,
            ZippedCount = _games.Count(g => g.ShouldZip),
            UploadedCount = _games.Count(g => g.ShouldUpload)
        };
    }
}
