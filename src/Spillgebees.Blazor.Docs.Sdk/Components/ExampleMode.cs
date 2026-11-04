namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Which pane of an <see cref="ExampleView{TComponent}"/> is shown.
/// </summary>
public enum ExampleMode
{
    /// <summary>The live component only.</summary>
    Preview,

    /// <summary>The extracted source only.</summary>
    Source,

    /// <summary>
    /// Both: the preview above the source. The source pane is height-capped and
    /// scrolls, so the preview stays in view.
    /// </summary>
    Split,
}

/// <summary>
/// The width the example preview is clamped to, for checking responsive
/// behaviour without resizing the browser.
/// </summary>
internal enum ExampleViewport
{
    /// <summary>Fill the available width.</summary>
    Full,

    /// <summary>Clamp to 768px.</summary>
    Medium,

    /// <summary>Clamp to 375px.</summary>
    Small,
}
