namespace APPID.Utilities;

/// <summary>
///     Provides utility methods for string manipulation operations.
/// </summary>
internal static class StringTools
{
    private static readonly Regex UrlParamRegex = new(@"([?&][^=&]+=[^&]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    ///     Removes everything after the first occurrence of a substring, keeping the substring.
    /// </summary>
    public static string RemoveEverythingAfterFirstKeepString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.IndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[..(index + removeMe.Length)] : s;
    }

    /// <summary>
    ///     Removes everything after the first occurrence of a substring, excluding the substring.
    /// </summary>
    public static string RemoveEverythingAfterFirstRemoveString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.IndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[..index] : s;
    }

    /// <summary>
    ///     Removes everything after the last occurrence of a substring, excluding the substring.
    /// </summary>
    public static string RemoveEverythingAfterLastRemoveString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.LastIndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[..index] : s;
    }

    /// <summary>
    ///     Removes everything after the last occurrence of a substring, keeping the substring.
    /// </summary>
    public static string RemoveEverythingAfterLastKeepString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.LastIndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[..(index + removeMe.Length)] : s;
    }

    /// <summary>
    ///     Removes everything before the first occurrence of a substring, keeping the substring.
    /// </summary>
    public static string RemoveEverythingBeforeFirstKeepString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.IndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[index..] : s;
    }

    /// <summary>
    ///     Removes everything before the first occurrence of a substring, excluding the substring.
    /// </summary>
    public static string RemoveEverythingBeforeFirstRemoveString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.IndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[(index + removeMe.Length)..] : s;
    }

    /// <summary>
    ///     Removes everything before the last occurrence of a substring, excluding the substring.
    /// </summary>
    public static string RemoveEverythingBeforeLastRemoveString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.LastIndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[(index + removeMe.Length)..] : s;
    }

    /// <summary>
    ///     Removes everything before the last occurrence of a substring, keeping the substring.
    /// </summary>
    public static string RemoveEverythingBeforeLastKeepString(string s, string removeMe)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(removeMe))
        {
            return s;
        }

        int index = s.LastIndexOf(removeMe, StringComparison.Ordinal);
        return index > 0 ? s[index..] : s;
    }

    /// <summary>
    ///     Extracts only numeric characters from a string.
    /// </summary>
    public static string KeepOnlyNumbers(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        return new string(s.Where(char.IsDigit).ToArray());
    }

    /// <summary>
    ///     Masks a sensitive value in a string by replacing it with asterisks.
    ///     Returns the original string if the sensitive value is null or empty.
    /// </summary>
    /// <param name="text">The text containing sensitive data.</param>
    /// <param name="sensitiveValue">The sensitive value to mask (e.g., API key, password).</param>
    /// <param name="mask">The replacement mask (default: "***").</param>
    /// <returns>The text with the sensitive value replaced by the mask.</returns>
    public static string MaskSensitiveData(string text, string sensitiveValue, string mask = "***")
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (string.IsNullOrEmpty(sensitiveValue))
        {
            return text;
        }

        return text.Replace(sensitiveValue, mask);
    }

    /// <summary>
    ///     Truncates a string to a maximum length, adding an ellipsis if truncated.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum length (including ellipsis).</param>
    /// <param name="ellipsis">The ellipsis string to append (default: "...").</param>
    /// <returns>The truncated string.</returns>
    public static string Truncate(string text, int maxLength, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        int truncateAt = maxLength - ellipsis.Length;
        return truncateAt > 0 ? text[..truncateAt] + ellipsis : text[..maxLength];
    }

    /// <summary>
    ///     Sanitizes a URL for logging by masking sensitive query parameters.
    /// </summary>
    /// <param name="url">The URL to sanitize.</param>
    /// <param name="sensitiveParams">List of query parameter names to mask (e.g., "key", "token", "apikey").</param>
    /// <returns>The URL with sensitive parameters masked.</returns>
    public static string SanitizeUrlForLogging(string url, params string[] sensitiveParams)
    {
        if (string.IsNullOrEmpty(url) || sensitiveParams == null || sensitiveParams.Length == 0)
        {
            return url;
        }

        string result = url;
        foreach (string param in sensitiveParams)
        {
            // Match patterns like: ?key=VALUE or &key=VALUE
            string pattern = $@"([?&]{Regex.Escape(param)}=)[^&]*";
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            result = regex.Replace(result, "$1***");
        }

        return result;
    }
}
