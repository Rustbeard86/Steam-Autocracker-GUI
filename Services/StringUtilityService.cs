using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of string utility service for text manipulation operations.
/// </summary>
public sealed class StringUtilityService : IStringUtilityService
{
    public string RemoveSpecialCharacters(string str)
    {
        return Regex.Replace(str, "[^a-zA-Z0-9._0-]+", " ", RegexOptions.Compiled);
    }

    public string SplitCamelCase(string input)
    {
        return Regex.Replace(input, "([A-Z])", " $1", RegexOptions.Compiled).Trim();
    }
}
