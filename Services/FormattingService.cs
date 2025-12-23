using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of formatting service for time and display utilities.
/// </summary>
public sealed class FormattingService : IFormattingService
{
    public string FormatEta(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:F0}s";
        }

        if (seconds < 3600)
        {
            return $"{(int)(seconds / 60)}:{(int)(seconds % 60):D2}";
        }

        return $"{(int)(seconds / 3600)}:{(int)(seconds % 3600 / 60):D2}:{(int)(seconds % 60):D2}";
    }

    public string FormatEtaLong(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            return "...";
        }

        if (seconds < 60)
        {
            return $"{(int)seconds}s";
        }

        if (seconds < 3600)
        {
            return $"{(int)(seconds / 60)}m {(int)(seconds % 60)}s";
        }

        int hours = (int)(seconds / 3600);
        int mins = (int)(seconds % 3600 / 60);
        return $"{hours}h {mins}m";
    }
}
