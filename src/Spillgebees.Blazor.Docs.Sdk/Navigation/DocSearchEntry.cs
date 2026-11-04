namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// The kind of thing a search result points at, used to group and label results.
/// </summary>
public enum DocSearchEntryKind
{
    /// <summary>A documentation page.</summary>
    Page,

    /// <summary>A type in the API reference.</summary>
    Type,

    /// <summary>A member of a type: parameter, method, or event.</summary>
    Member,

    /// <summary>A section within a page.</summary>
    Section,
}

/// <summary>
/// A single entry in the search index backing the command palette.
/// </summary>
/// <param name="Title">The primary text shown and matched against.</param>
/// <param name="Href">Where selecting the result navigates to.</param>
/// <param name="Group">The heading this result is listed under.</param>
/// <param name="Kind">What the entry points at.</param>
/// <param name="Description">
/// Optional secondary text. Also matched against, at a lower rank than
/// <paramref name="Title"/>.
/// </param>
public sealed record DocSearchEntry(
    string Title,
    string Href,
    string Group,
    DocSearchEntryKind Kind = DocSearchEntryKind.Page,
    string? Description = null
)
{
    /// <summary>
    /// Builds search entries from the sidebar navigation, one per page.
    /// </summary>
    /// <param name="navigation">The sections shown in the sidebar.</param>
    /// <returns>One entry per page, preserving navigation order.</returns>
    public static IReadOnlyList<DocSearchEntry> FromNavigation(IReadOnlyList<NavSection> navigation) =>
        navigation
            .SelectMany(section =>
                section.Pages.Select(page => new DocSearchEntry(page.Title, page.Href, section.Title, page.Kind))
            )
            .ToList();
}
