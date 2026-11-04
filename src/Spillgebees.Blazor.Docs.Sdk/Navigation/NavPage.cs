namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Represents a single navigable page in the documentation sidebar.
/// </summary>
/// <param name="Title">The display title of the page.</param>
/// <param name="Href">The URL path of the page.</param>
/// <param name="Kind">
/// What the page documents, which decides its icon and label in search.
/// <see cref="ApiReferenceNav"/> marks its pages as <see cref="DocSearchEntryKind.Type"/>.
/// </param>
public sealed record NavPage(string Title, string Href, DocSearchEntryKind Kind = DocSearchEntryKind.Page);
