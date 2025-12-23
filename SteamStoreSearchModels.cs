namespace SteamAutocrackGUI;

/// <summary>
///     Response from the Steam Store search API.
/// </summary>
internal sealed class SteamStoreSearchResponse
{
    public int total { get; set; }
    public List<SteamStoreSearchItem>? items { get; set; }
}

/// <summary>
///     Individual item in a Steam Store search response.
/// </summary>
internal sealed class SteamStoreSearchItem
{
    public string? type { get; set; }
    public string? name { get; set; }
    public int id { get; set; }
}
