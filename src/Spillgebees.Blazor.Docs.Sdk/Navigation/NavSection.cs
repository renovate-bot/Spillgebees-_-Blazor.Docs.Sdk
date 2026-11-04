namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Represents a section in the documentation sidebar, grouping related pages under a common title.
/// </summary>
/// <param name="Title">The display title of the section.</param>
/// <param name="Pages">The pages contained within this section.</param>
/// <param name="Collapsible">
/// Whether the section can be expanded and collapsed. Long generated sections,
/// an API reference in particular, otherwise push the hand-written navigation
/// out of view.
/// </param>
/// <param name="InitiallyExpanded">
/// Whether a collapsible section starts open. Ignored when
/// <paramref name="Collapsible"/> is <see langword="false"/>. A section
/// containing the current page is always expanded regardless.
/// </param>
public sealed record NavSection(
    string Title,
    IReadOnlyList<NavPage> Pages,
    bool Collapsible = false,
    bool InitiallyExpanded = true
)
{
    /// <summary>
    /// Creates a collapsed-by-default section, the usual choice for a generated
    /// API reference.
    /// </summary>
    /// <param name="title">The display title of the section.</param>
    /// <param name="pages">The pages contained within this section.</param>
    public static NavSection Collapsed(string title, IReadOnlyList<NavPage> pages) =>
        new(title, pages, Collapsible: true, InitiallyExpanded: false);
}
