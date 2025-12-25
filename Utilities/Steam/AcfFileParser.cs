namespace APPID.Utilities.Steam;

/// <summary>
///     Centralized parser for Steam ACF (App Configuration File) format.
///     Eliminates duplicate parsing logic across the codebase.
/// </summary>
public static class AcfFileParser
{
    /// <summary>
    ///     Parses an ACF file into a flat dictionary of key-value pairs.
    ///     Only captures top-level keys (ignores nested structures).
    /// </summary>
    /// <param name="content">The raw ACF file content</param>
    /// <returns>Dictionary of key-value pairs</returns>
    public static Dictionary<string, string> ParseFlat(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Regular expression for parsing VDF/ACF format: "key" "value"
        var keyValuePattern = @"""(\w+)""\s+""([^""]*)""";
        var matches = Regex.Matches(content, keyValuePattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                // Only store the first occurrence of each key (ignores nested structures)
                result.TryAdd(key, value);
            }
        }

        return result;
    }

    /// <summary>
    ///     Parses a specific section from an ACF file.
    ///     Useful for extracting nested data like InstalledDepots.
    /// </summary>
    /// <param name="content">The raw ACF file content</param>
    /// <param name="sectionName">The section name to extract (e.g., "InstalledDepots")</param>
    /// <returns>The content of the section, or null if not found</returns>
    public static string? GetSection(string content, string sectionName)
    {
        try
        {
            // Find the section: "SectionName" { ... }
            var sectionPattern = $@"""{sectionName}""\s*\{{([\s\S]*?)\n\t\}}";
            var match = Regex.Match(content, sectionPattern, RegexOptions.Multiline);

            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Parses the InstalledDepots section from an ACF file.
    ///     Returns depot ID -> (manifest ID, size) mappings.
    /// </summary>
    /// <param name="content">The raw ACF file content</param>
    /// <returns>Dictionary of depot information</returns>
    public static Dictionary<string, (string manifestId, long size)> ParseInstalledDepots(string content)
    {
        var depots = new Dictionary<string, (string manifestId, long size)>();

        try
        {
            string? depotsSection = GetSection(content, "InstalledDepots");
            if (string.IsNullOrEmpty(depotsSection))
            {
                return depots;
            }

            // Parse each depot entry: "depotId" { "manifest" "manifestId" "size" "sizeBytes" }
            var depotMatches = Regex.Matches(depotsSection,
                @"""(\d+)""\s*\{[^}]*""manifest""\s+""(\d+)""[^}]*""size""\s+""(\d+)""",
                RegexOptions.Multiline);

            foreach (Match match in depotMatches)
            {
                if (match.Groups.Count >= 4)
                {
                    string depotId = match.Groups[1].Value;
                    string manifestId = match.Groups[2].Value;
                    long size = 0;
                    long.TryParse(match.Groups[3].Value, out size);

                    depots[depotId] = (manifestId, size);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ACF PARSER] Error parsing depots: {ex.Message}");
        }

        return depots;
    }

    /// <summary>
    ///     Extracts the AppID from an ACF file name (e.g., "appmanifest_480.acf" -> "480").
    /// </summary>
    /// <param name="acfFileName">The ACF file name</param>
    /// <returns>The AppID, or null if invalid format</returns>
    public static string? ExtractAppIdFromFileName(string acfFileName)
    {
        try
        {
            var match = Regex.Match(acfFileName, @"appmanifest_(\d+)\.acf");
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Validates if a string contains valid ACF/VDF format.
    /// </summary>
    /// <param name="content">The content to validate</param>
    /// <returns>True if valid ACF format</returns>
    public static bool IsValidAcfFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        // Check for basic ACF structure: has quoted keys and values
        return content.Contains("\"") && content.Contains("{") && content.Contains("}");
    }

    /// <summary>
    ///     Extracts all key-value pairs from a nested section.
    ///     More flexible than ParseFlat for working with specific sections.
    /// </summary>
    /// <param name="sectionContent">The section content (from GetSection)</param>
    /// <returns>Dictionary of key-value pairs within the section</returns>
    public static Dictionary<string, string> ParseSectionContent(string sectionContent)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sectionContent))
        {
            return result;
        }

        var keyValuePattern = @"""(\w+)""\s+""([^""]*)""";
        var matches = Regex.Matches(sectionContent, keyValuePattern);

        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3)
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                result.TryAdd(key, value);
            }
        }

        return result;
    }
}
