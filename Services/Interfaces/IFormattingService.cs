namespace APPID.Services.Interfaces;

/// <summary>
///     Service for formatting time and other display-related utilities.
/// </summary>
public interface IFormattingService
{
    /// <summary>
    ///     Formats a time duration in seconds to a short string (e.g., "5s", "1:30", "2:15:45").
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>A formatted time string.</returns>
    string FormatEta(double seconds);

    /// <summary>
    ///     Formats a time duration in seconds to a long descriptive string (e.g., "5 seconds", "2m 30s", "1h 15m").
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>A formatted time string.</returns>
    string FormatEtaLong(double seconds);
}
