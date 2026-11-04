namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// The visitor's colour scheme preference, independent of the
/// <see cref="DocsTheme"/> in use.
/// </summary>
/// <remarks>
/// This is a <em>preference</em>, not the applied scheme:
/// <see cref="System"/> resolves to light or dark from the operating system and
/// follows it live. The resolved value is written to <c>data-docs-scheme</c>.
/// </remarks>
public enum DocsColorScheme
{
    /// <summary>Follow the operating system, updating when it changes.</summary>
    System,

    /// <summary>Always use the light scheme.</summary>
    Light,

    /// <summary>Always use the dark scheme.</summary>
    Dark,
}

/// <summary>
/// Conversions between <see cref="DocsColorScheme"/> and the string values used
/// by the JavaScript module and the <c>data-docs-scheme</c> attribute.
/// </summary>
internal static class DocsColorSchemeExtensions
{
    /// <summary>Converts to the wire value used by the JavaScript module.</summary>
    public static string ToJsValue(this DocsColorScheme scheme) =>
        scheme switch
        {
            DocsColorScheme.Light => "light",
            DocsColorScheme.Dark => "dark",
            _ => "system",
        };

    /// <summary>
    /// Parses a wire value, falling back to <see cref="DocsColorScheme.System"/>
    /// for anything unrecognised.
    /// </summary>
    public static DocsColorScheme ParseColorScheme(string? value) =>
        value switch
        {
            "light" => DocsColorScheme.Light,
            "dark" => DocsColorScheme.Dark,
            _ => DocsColorScheme.System,
        };
}
