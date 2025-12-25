using System.Data;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implements progressive game search with multiple fallback strategies.
/// </summary>
public class GameSearchService : IGameSearchService
{
    private static readonly Regex CamelCaseRegex =
        new(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

    private static readonly Regex SpecialCharsRegex = new(@"[^a-zA-Z0-9._0-]+", RegexOptions.Compiled);

    public SearchResult PerformSearch(string searchText, DataTable dataTable)
    {
        if (string.IsNullOrWhiteSpace(searchText) || dataTable == null)
        {
            return new SearchResult { Quality = SearchMatchQuality.NoMatch };
        }

        var view = dataTable.DefaultView;

        // Level 1: Exact match detection (case-insensitive)
        if (TryExactMatch(searchText, dataTable, view, out var exactResult))
        {
            return exactResult;
        }

        // Level 1.5: CamelCase split exact match (e.g., "RSDragonwilds" -> "RS Dragonwilds")
        if (TryCamelCaseSplitMatch(searchText, dataTable, view, out var camelCaseResult))
        {
            return camelCaseResult;
        }

        // Level 2: Soundtrack/DLC filtering (prefer base games)
        if (TryBaseGameMatch(searchText, dataTable, view, out var baseGameResult))
        {
            return baseGameResult;
        }

        // Level 3: Unicode normalization
        if (TryNormalizedMatch(searchText, dataTable, view, out var normalizedResult))
        {
            return normalizedResult;
        }

        // Level 4: Progressive string cleaning and pattern matching
        return PerformProgressiveSearch(searchText, view);
    }

    public SearchMatchQuality GetMatchQuality(int resultCount, string searchText, DataTable filteredData)
    {
        if (resultCount == 0)
        {
            return SearchMatchQuality.NoMatch;
        }

        if (resultCount == 1)
        {
            // Check if it's truly an exact match
            var row = filteredData.DefaultView[0].Row;
            var gameName = row.Field<string>("Name");
            if (string.Equals(gameName, searchText, StringComparison.OrdinalIgnoreCase))
            {
                return SearchMatchQuality.ExactMatch;
            }

            return SearchMatchQuality.SingleMatch;
        }

        return SearchMatchQuality.MultipleMatches;
    }

    #region Private Helper Methods

    private bool TryExactMatch(string searchText, DataTable dataTable, DataView view, out SearchResult result)
    {
        result = null;

        if (searchText.Length <= 2)
        {
            return false;
        }

        // Check for exact case-insensitive matches
        var exactMatches = dataTable.AsEnumerable()
            .Where(row => string.Equals(row.Field<string>("Name"), searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exactMatches.Any())
        {
            string exactName = exactMatches.First().Field<string>("Name");
            view.RowFilter = $"Name = '{EscapeSingleQuotes(exactName)}'";

            result = new SearchResult
            {
                MatchCount = 1,
                Quality = SearchMatchQuality.ExactMatch,
                AppliedFilter = view.RowFilter,
                HasExactMatch = true
            };
            return true;
        }

        return false;
    }

    private bool TryCamelCaseSplitMatch(string searchText, DataTable dataTable, DataView view, out SearchResult result)
    {
        result = null;

        if (searchText.Length <= 3 || !HasCamelCasePattern(searchText))
        {
            return false;
        }

        string splitSearch = SplitCamelCase(searchText);

        if (splitSearch == searchText)
        {
            return false;
        }

        var camelCaseMatches = dataTable.AsEnumerable()
            .Where(row => string.Equals(row.Field<string>("Name"), splitSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (camelCaseMatches.Any())
        {
            string exactName = camelCaseMatches.First().Field<string>("Name");
            view.RowFilter = $"Name = '{EscapeSingleQuotes(exactName)}'";

            result = new SearchResult
            {
                MatchCount = 1,
                Quality = SearchMatchQuality.ExactMatch,
                AppliedFilter = view.RowFilter,
                HasExactMatch = true
            };
            return true;
        }

        return false;
    }

    private bool TryBaseGameMatch(string searchText, DataTable dataTable, DataView view, out SearchResult result)
    {
        result = null;

        if (searchText.Length <= 3)
        {
            return false;
        }

        var partialMatches = dataTable.AsEnumerable()
            .Where(row => row.Field<string>("Name")?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (!partialMatches.Any())
        {
            return false;
        }

        // Filter out soundtracks, DLC, demos, etc.
        var baseGameMatches = partialMatches.Where(row =>
            {
                string name = row.Field<string>("Name")?.ToLower() ?? "";
                return !name.Contains("soundtrack") && !name.Contains("dlc") &&
                       !name.Contains("season pass") && !name.Contains("expansion") &&
                       !name.Contains("demo") && !name.Contains("beta");
            })
            .OrderBy(row => row.Field<string>("Name")?.Length ?? int.MaxValue)
            .ToList();

        if (baseGameMatches.Any())
        {
            string baseGameName = baseGameMatches.First().Field<string>("Name");
            view.RowFilter = $"Name = '{EscapeSingleQuotes(baseGameName)}'";

            result = new SearchResult
            {
                MatchCount = 1,
                Quality = SearchMatchQuality.ExactMatch,
                AppliedFilter = view.RowFilter,
                HasExactMatch = true
            };
            return true;
        }

        return false;
    }

    private bool TryNormalizedMatch(string searchText, DataTable dataTable, DataView view, out SearchResult result)
    {
        result = null;

        var normalizedSearch = searchText.Normalize(NormalizationForm.FormKC);
        var normalizedMatches = dataTable.AsEnumerable()
            .Where(row => string.Equals(
                row.Field<string>("Name")?.Normalize(NormalizationForm.FormKC),
                normalizedSearch,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (normalizedMatches.Any())
        {
            string exactName = normalizedMatches.First().Field<string>("Name");
            view.RowFilter = $"Name = '{EscapeSingleQuotes(exactName)}'";

            result = new SearchResult
            {
                MatchCount = 1,
                Quality = SearchMatchQuality.ExactMatch,
                AppliedFilter = view.RowFilter,
                HasExactMatch = true
            };
            return true;
        }

        return false;
    }

    private SearchResult PerformProgressiveSearch(string searchText, DataView view)
    {
        string cleaned = CleanSearchText(searchText);
        string splitSearch = SplitCamelCase(cleaned);

        // Level 1: Exact match on cleaned text
        if (TryApplyFilter(view, $"Name LIKE '{cleaned.Replace("_", "")}'"))
        {
            return CreateResult(view);
        }

        // Level 2: CamelCase split match
        if (TryApplyFilter(view, $"Name LIKE '{splitSearch.Replace("_", "")}'"))
        {
            return CreateResult(view);
        }

        // Level 3: Partial word matching with AND
        string partialFilter = BuildPartialFilter(cleaned);
        if (TryApplyFilter(view, partialFilter))
        {
            return CreateResult(view);
        }

        // Level 4: Fuzzy matching (significant words)
        if (TryFuzzySearch(cleaned, view))
        {
            return CreateResult(view);
        }

        // Level 5: Special character prefix search (e.g., "$game")
        if (searchText.StartsWith("$"))
        {
            string specialSearch = searchText[1..].Replace("&", "and").Replace(":", " -");
            if (TryApplyFilter(view, $"Name LIKE '{specialSearch}'"))
            {
                return CreateResult(view);
            }
        }

        return new SearchResult { MatchCount = 0, Quality = SearchMatchQuality.NoMatch };
    }

    private bool TryApplyFilter(DataView view, string filter)
    {
        try
        {
            view.RowFilter = filter;
            return view.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private bool TryFuzzySearch(string searchText, DataView view)
    {
        var words = searchText.Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .OrderByDescending(w => w.Length)
            .Take(2);

        foreach (string word in words)
        {
            if (TryApplyFilter(view, $"Name LIKE '%{word}%'"))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildPartialFilter(string searchText)
    {
        string cleaned = searchText.Replace("_", "").Replace(" and ", " ").Replace(" the ", " ").Replace(":", "");
        return $"Name LIKE '%{cleaned.Replace(" ", "%' AND Name LIKE '%")}%'";
    }

    private SearchResult CreateResult(DataView view)
    {
        var quality = view.Count switch
        {
            0 => SearchMatchQuality.NoMatch,
            1 => SearchMatchQuality.SingleMatch,
            _ => SearchMatchQuality.MultipleMatches
        };

        return new SearchResult { MatchCount = view.Count, Quality = quality, AppliedFilter = view.RowFilter };
    }

    private static string CleanSearchText(string text)
    {
        return SpecialCharsRegex.Replace(text.ToLower(), " ").Trim();
    }

    private static string SplitCamelCase(string input)
    {
        return CamelCaseRegex.Replace(input, " ").Trim();
    }

    private static bool HasCamelCasePattern(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        bool hasLower = false;
        bool hasUpper = false;

        foreach (char c in text)
        {
            if (char.IsLower(c))
            {
                hasLower = true;
            }
            else if (char.IsUpper(c))
            {
                hasUpper = true;
            }

            if (hasLower && hasUpper)
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeSingleQuotes(string text)
    {
        return text?.Replace("'", "''") ?? string.Empty;
    }

    #endregion
}
