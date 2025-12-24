namespace APPID.Services.Interfaces;

/// <summary>
///     Service for string utility operations.
/// </summary>
public interface IStringUtilityService
{
    /// <summary>
    ///     Removes special characters from a string, keeping only alphanumeric characters, dots, and underscores.
    /// </summary>
    /// <param name="str">The input string to process.</param>
    /// <returns>A string with special characters replaced by spaces.</returns>
    string RemoveSpecialCharacters(string str);

    /// <summary>
    ///     Splits a camel case string by inserting spaces before capital letters.
    /// </summary>
    /// <param name="input">The camel case string to split.</param>
    /// <returns>A space-separated string.</returns>
    string SplitCamelCase(string input);
}
