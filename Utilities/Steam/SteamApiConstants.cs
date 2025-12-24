namespace APPID.Utilities.Steam;

/// <summary>
///     Centralized constants for Steam API endpoints and configuration.
///     Prevents magic strings throughout the codebase.
/// </summary>
public static class SteamApiConstants
{
    // === Base URLs ===
    public const string SteamStoreBaseUrl = "https://store.steampowered.com";
    public const string SteamWebApiBaseUrl = "https://api.steampowered.com";
    public const string SteamCommunityBaseUrl = "https://steamcommunity.com";

    // === Store API Endpoints ===
    public const string AppDetailsEndpoint = "/api/appdetails";
    public const string StoreSearchEndpoint = "/api/storesearch";
    public const string FeaturedEndpoint = "/api/featured";
    public const string FeaturedCategoriesEndpoint = "/api/featuredcategories";

    // === Web API Endpoints ===
    public const string GetSchemaForGameEndpoint = "/ISteamUserStats/GetSchemaForGame/v2/";
    public const string GetPlayerAchievementsEndpoint = "/ISteamUserStats/GetPlayerAchievements/v1/";
    public const string GetGlobalAchievementPercentagesEndpoint = "/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/";

    // === Query Parameters ===
    public const string ParamAppId = "appid";
    public const string ParamAppIds = "appids";
    public const string ParamKey = "key";
    public const string ParamLanguage = "l";
    public const string ParamCountryCode = "cc";
    public const string ParamTerm = "term";

    // === Default Values ===
    public const string DefaultLanguage = "en";
    public const string DefaultCountryCode = "us";

    // === Filter Keywords (for excluding non-game content) ===
    public static readonly string[] ExcludedContentKeywords =
    [
        "soundtrack",
        "dlc",
        "demo",
        "beta",
        "test",
        "server",
        "playtest",
        "dedicated",
        "sdk",
        "editor",
        "tool",
        "artbook",
        "art book",
        "original score",
        "ost"
    ];

    // === App Types ===
    public const string AppTypeGame = "game";
    public const string AppTypeDlc = "dlc";
    public const string AppTypeDemo = "demo";
    public const string AppTypeMod = "mod";
    public const string AppTypeVideo = "video";

    // === Rate Limiting ===
    public const int RateLimitDelayMs = 100; // Delay between API calls to avoid throttling
    public const int MaxRetriesOnRateLimit = 3;

    // === Timeouts ===
    public const int DefaultTimeoutSeconds = 30;
    public const int LongOperationTimeoutSeconds = 60;

    // === Cache Settings ===
    public const int CacheDurationMinutes = 5; // Memory cache TTL for API responses
    public const int PersistentCacheDurationDays = 7; // Disk cache TTL

    /// <summary>
    ///     Builds the full URL for app details API
    /// </summary>
    /// <param name="appId">The Steam App ID</param>
    /// <param name="countryCode">Optional country code (default: us)</param>
    /// <param name="language">Optional language code (default: en)</param>
    /// <returns>Full API URL</returns>
    public static string BuildAppDetailsUrl(string appId, string? countryCode = null, string? language = null)
    {
        string cc = countryCode ?? DefaultCountryCode;
        string lang = language ?? DefaultLanguage;
        return $"{SteamStoreBaseUrl}{AppDetailsEndpoint}?{ParamAppIds}={appId}&{ParamCountryCode}={cc}&{ParamLanguage}={lang}";
    }

    /// <summary>
    ///     Builds the full URL for store search API
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <param name="countryCode">Optional country code (default: us)</param>
    /// <param name="language">Optional language code (default: en)</param>
    /// <returns>Full API URL</returns>
    public static string BuildStoreSearchUrl(string searchTerm, string? countryCode = null, string? language = null)
    {
        string cc = countryCode ?? DefaultCountryCode;
        string lang = language ?? DefaultLanguage;
        string encodedTerm = Uri.EscapeDataString(searchTerm);
        return $"{SteamStoreBaseUrl}{StoreSearchEndpoint}?{ParamTerm}={encodedTerm}&{ParamCountryCode}={cc}&{ParamLanguage}={lang}";
    }

    /// <summary>
    ///     Builds the full URL for GetSchemaForGame API
    /// </summary>
    /// <param name="appId">The Steam App ID</param>
    /// <param name="apiKey">Optional Steam Web API key</param>
    /// <param name="language">Optional language code (default: en)</param>
    /// <returns>Full API URL</returns>
    public static string BuildSchemaForGameUrl(string appId, string? apiKey = null, string? language = null)
    {
        string lang = language ?? DefaultLanguage;
        string url = $"{SteamWebApiBaseUrl}{GetSchemaForGameEndpoint}?{ParamAppId}={appId}&{ParamLanguage}={lang}";

        if (!string.IsNullOrEmpty(apiKey))
        {
            url += $"&{ParamKey}={apiKey}";
        }

        return url;
    }

    /// <summary>
    ///     Checks if a game name contains excluded content keywords
    /// </summary>
    /// <param name="gameName">The game name to check</param>
    /// <returns>True if the name contains excluded keywords</returns>
    public static bool ContainsExcludedKeyword(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return false;
        }

        string nameLower = gameName.ToLowerInvariant();
        return ExcludedContentKeywords.Any(keyword => nameLower.Contains(keyword));
    }
}
