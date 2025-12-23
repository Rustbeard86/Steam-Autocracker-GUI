namespace APPID;

/// <summary>
///     Provides utility methods for string manipulation operations.
/// </summary>
internal static class StringTools
{
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
}
