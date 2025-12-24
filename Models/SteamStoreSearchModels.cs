namespace APPID.Models;

/// <summary>
///     Response from the Steam Store search API.
/// </summary>
internal sealed class SteamStoreSearchResponse
{
    public int Total { get; set; }
    public List<SteamStoreSearchItem>? Items { get; set; }
}

/// <summary>
///     Individual item in a Steam Store search response.
/// </summary>
internal sealed class SteamStoreSearchItem
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public int Id { get; set; }
}
