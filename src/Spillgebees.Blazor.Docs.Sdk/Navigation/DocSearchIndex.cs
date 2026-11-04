namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Ranks <see cref="DocSearchEntry"/> values against a query for the command
/// palette.
/// </summary>
/// <remarks>
/// Ranking is deliberately simple and predictable rather than fuzzy: an exact
/// title match beats a title prefix, which beats a title substring, which beats
/// a description substring. Ties keep the original navigation order, so results
/// stay stable as the visitor types.
/// </remarks>
public static class DocSearchIndex
{
    private const int ExactTitle = 0;
    private const int TitlePrefix = 1;
    private const int TitleSubstring = 2;
    private const int DescriptionSubstring = 3;
    private const int NoMatch = -1;

    // results shown when the query is empty
    private const int BlankQueryLimit = 8;

    /// <summary>
    /// Filters and ranks <paramref name="entries"/> against <paramref name="query"/>.
    /// </summary>
    /// <param name="entries">The full search index.</param>
    /// <param name="query">
    /// The visitor's query. When empty, the first <paramref name="limit"/> page
    /// entries are returned as a starting point rather than nothing.
    /// </param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <returns>Matching entries, best match first.</returns>
    public static IReadOnlyList<DocSearchEntry> Search(
        IReadOnlyList<DocSearchEntry> entries,
        string? query,
        int limit = 12
    )
    {
        var term = query?.Trim();

        if (string.IsNullOrEmpty(term))
        {
            return entries
                .Where(entry => entry.Kind == DocSearchEntryKind.Page)
                .Take(Math.Min(limit, BlankQueryLimit))
                .ToList();
        }

        return entries
            .Select((entry, order) => (entry, order, rank: Rank(entry, term)))
            .Where(candidate => candidate.rank != NoMatch)
            .OrderBy(candidate => candidate.rank)
            .ThenBy(candidate => candidate.order)
            .Take(limit)
            .Select(candidate => candidate.entry)
            .ToList();
    }

    private static int Rank(DocSearchEntry entry, string term)
    {
        if (string.Equals(entry.Title, term, StringComparison.OrdinalIgnoreCase))
        {
            return ExactTitle;
        }

        if (entry.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
        {
            return TitlePrefix;
        }

        if (entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return TitleSubstring;
        }

        if (entry.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
        {
            return DescriptionSubstring;
        }

        return NoMatch;
    }

    /// <summary>
    /// Splits <paramref name="text"/> around the first case-insensitive
    /// occurrence of <paramref name="term"/>, so the match can be emphasised.
    /// </summary>
    /// <returns>
    /// The text before the match, the matched text as it appears in
    /// <paramref name="text"/>, and the text after. When there is no match, the
    /// whole string is returned as the prefix.
    /// </returns>
    public static (string Before, string Match, string After) SplitOnMatch(string text, string? term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return (text, string.Empty, string.Empty);
        }

        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? (text, string.Empty, string.Empty)
            : (text[..index], text.Substring(index, term.Length), text[(index + term.Length)..]);
    }
}
