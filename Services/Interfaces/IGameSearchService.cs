using System.Data;

namespace APPID.Services.Interfaces;

/// <summary>
///     Service for searching and filtering Steam game data.
/// </summary>
public interface IGameSearchService
{
    /// <summary>
    ///     Performs a progressive search against the Steam games database with multiple fallback strategies.
    /// </summary>
    /// <param name="searchText">The text to search for.</param>
    /// <param name="dataTable">The DataTable containing Steam game data.</param>
    /// <returns>A SearchResult containing the filtered data and match quality.</returns>
    SearchResult PerformSearch(string searchText, DataTable dataTable);

    /// <summary>
    ///     Determines if a search result represents an exact match suitable for auto-selection.
    /// </summary>
    SearchMatchQuality GetMatchQuality(int resultCount, string searchText, DataTable filteredData);
}

/// <summary>
///     Result of a game search operation.
/// </summary>
public class SearchResult
{
    public int MatchCount { get; set; }
    public SearchMatchQuality Quality { get; set; }
    public string AppliedFilter { get; set; }
    public bool HasExactMatch { get; set; }
}

/// <summary>
///     Quality classification of search matches.
/// </summary>
public enum SearchMatchQuality
{
    NoMatch,
    PartialMatch,
    SingleMatch,
    MultipleMatches,
    ExactMatch
}
