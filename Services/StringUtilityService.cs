using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implementation of string utility service for text manipulation operations.
/// </summary>
public sealed class StringUtilityService : IStringUtilityService
{
    private static readonly Regex SpecialCharsRegex = new(@"[^a-zA-Z0-9._0-]+", RegexOptions.Compiled);
    private static readonly Regex CamelCaseRegex = new(@"([A-Z])", RegexOptions.Compiled);

    public string RemoveSpecialCharacters(string str)
    {
        return SpecialCharsRegex.Replace(str, " ");
    }

    public string SplitCamelCase(string input)
    {
        return CamelCaseRegex.Replace(input, " $1").Trim();
    }
}
