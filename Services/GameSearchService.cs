using System.Data;
using APPID.Services.Interfaces;

namespace APPID.Services;

/// <summary>
///     Implements progressive game search with multiple fallback strategies.
/// </summary>
public partial class GameSearchService : IGameSearchService
{
    // Updated regex to properly split CamelCase with better handling of consecutive capitals:
    // - "wordWord" → "word Word" (lowercase followed by uppercase)
    // - "WORDWord" → "WORD Word" (multiple uppercase followed by capital+word)  
    // The third pattern ensures we split BEFORE the last capital in a sequence of capitals
    private static readonly Regex CamelCaseRegex =
        GenCamelCaseRegex();

    private static readonly Regex SpecialCharsRegex = GenSpecialCharsRegex();

    public SearchResult PerformSearch(string searchText, DataTable? dataTable)
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

        // Level 1.5: CamelCase split exact match
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

    public string NormalizeGameNameForSearch(string rawGameName)
    {
        if (string.IsNullOrWhiteSpace(rawGameName))
        {
            return rawGameName;
        }

        if (HasCamelCasePattern(rawGameName))
        {
            string split = SplitCamelCase(rawGameName);
            // Now clean the split result
            return CleanSearchText(split);
        }

        // If no CamelCase detected, just clean it
        return CleanSearchText(rawGameName);
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

        if (exactMatches.Count != 0)
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

        // Try exact match first with the split version
        var camelCaseMatches = dataTable.AsEnumerable()
            .Where(row => string.Equals(row.Field<string>("Name"), splitSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (camelCaseMatches.Count != 0)
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

        // Try partial word matching - e.g., "RS Dragonwilds" should match "RuneScape Dragonwilds"
        // Split the camelCase result into words and search for games containing all those words
        var searchWords = splitSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1) // Skip single letters unless it's the only word
            .ToList();

        if (searchWords.Count > 0)
        {
            // Build filter that requires all words to be present
            string wordFilter = string.Join(" AND ",
                searchWords.Select(word => $"Name LIKE '%{EscapeSingleQuotes(word)}%'"));

            try
            {
                view.RowFilter = wordFilter;
                if (view.Count > 0)
                {
                    // Prefer games that start with the first word
                    var firstWord = searchWords[0];
                    var bestMatch = dataTable.AsEnumerable()
                        .Where(row =>
                            row.Field<string>("Name")?.Contains(firstWord, StringComparison.OrdinalIgnoreCase) == true)
                        .OrderBy(row =>
                        {
                            string name = row.Field<string>("Name") ?? "";
                            // Prioritize names that start with the first word
                            if (name.StartsWith(firstWord, StringComparison.OrdinalIgnoreCase))
                            {
                                return 0;
                            }

                            // Then names where first word appears after a space (start of a word)
                            if (name.Contains($" {firstWord}", StringComparison.OrdinalIgnoreCase))
                            {
                                return 1;
                            }

                            return 2;
                        })
                        .ThenBy(row => row.Field<string>("Name")?.Length ?? int.MaxValue)
                        .FirstOrDefault();

                    if (bestMatch != null)
                    {
                        string bestMatchName = bestMatch.Field<string>("Name");
                        view.RowFilter = $"Name = '{EscapeSingleQuotes(bestMatchName)}'";

                        result = new SearchResult
                        {
                            MatchCount = 1,
                            Quality = SearchMatchQuality.ExactMatch,
                            AppliedFilter = view.RowFilter,
                            HasExactMatch = true
                        };
                        return true;
                    }

                    // If no best match found but we have results, return them
                    result = new SearchResult
                    {
                        MatchCount = view.Count,
                        Quality = view.Count == 1
                            ? SearchMatchQuality.SingleMatch
                            : SearchMatchQuality.MultipleMatches,
                        AppliedFilter = view.RowFilter,
                        HasExactMatch = false
                    };
                    return true;
                }
            }
            catch
            {
                // If filter fails, continue with default search
            }
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

        if (partialMatches.Count == 0)
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

        if (baseGameMatches.Count != 0)
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

        if (normalizedMatches.Count != 0)
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

    [GeneratedRegex(@"[^a-zA-Z0-9._0-]+", RegexOptions.Compiled)]
    private static partial Regex GenSpecialCharsRegex();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])|(?<=\w)(?=[A-Z][a-z])", RegexOptions.Compiled)]
    private static partial Regex GenCamelCaseRegex();

    #endregion
}
