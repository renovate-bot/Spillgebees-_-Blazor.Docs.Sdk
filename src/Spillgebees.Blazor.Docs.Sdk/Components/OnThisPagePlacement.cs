namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Where an <see cref="OnThisPage"/> instance renders.
/// </summary>
/// <remarks>
/// The two placements are shown at different breakpoints and are mutually
/// exclusive, so rendering both is what keeps the section list reachable on
/// every viewport.
/// </remarks>
public enum OnThisPagePlacement
{
    /// <summary>
    /// A sticky rail beside the content. Hidden below the wide breakpoint.
    /// </summary>
    Rail,

    /// <summary>
    /// A collapsed disclosure above the content. Shown only below the wide
    /// breakpoint, where there is no room for the rail.
    /// </summary>
    Inline,
}
