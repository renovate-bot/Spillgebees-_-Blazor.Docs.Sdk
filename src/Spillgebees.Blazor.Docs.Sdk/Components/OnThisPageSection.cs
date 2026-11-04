namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// A heading discovered on the current page, used to build the on-this-page rail.
/// </summary>
/// <param name="Title">The heading text.</param>
/// <param name="Id">The heading's element id, used as the anchor target.</param>
/// <param name="Level">
/// The heading level: 2 for <c>&lt;h2&gt;</c>, 3 for <c>&lt;h3&gt;</c>. Used to
/// indent nested entries.
/// </param>
public sealed record OnThisPageSection(string Title, string Id, int Level = 2);
