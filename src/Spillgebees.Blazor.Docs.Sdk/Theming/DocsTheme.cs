using System.Text.RegularExpressions;

namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// A docs theme: the palette and type family applied to the site, independent of
/// the light/dark colour scheme.
/// </summary>
/// <remarks>
/// <para>
/// The theme name is written to the <c>data-docs-theme</c> attribute on the
/// document element, which the stylesheet keys off. Use one of the built-in
/// themes, or <see cref="Custom"/> together with your own stylesheet:
/// </para>
/// <code>
/// &lt;DocSite Theme="@DocsTheme.Custom(&quot;acme&quot;)" ... /&gt;
/// </code>
/// <para>
/// A custom stylesheet only needs to redefine the design tokens it cares about;
/// anything omitted falls back to <see cref="Refined"/>. Copy
/// <c>_content/Spillgebees.Blazor.Docs.Sdk/docs-sdk-theme-template.css</c> as a
/// starting point that documents every token.
/// </para>
/// </remarks>
public sealed partial class DocsTheme : IEquatable<DocsTheme>
{
    /// <summary>
    /// Warm editorial dark with a cream light scheme. The default theme.
    /// </summary>
    public static DocsTheme Refined { get; } = new("refined", "Refined");

    /// <summary>
    /// High-density gold-on-black, monospace-forward, square corners.
    /// </summary>
    public static DocsTheme Terminal { get; } = new("terminal", "Terminal");

    /// <summary>
    /// Ink on parchment: monochrome sepia, inverted selection bars, square
    /// bullets and corner-bracket frames.
    /// </summary>
    public static DocsTheme GloryToMankind { get; } = new("glory-to-mankind", "Glory to Mankind");

    /// <summary>
    /// The themes shipped with this package, in presentation order.
    /// </summary>
    public static IReadOnlyList<DocsTheme> BuiltIn { get; } = [Refined, Terminal, GloryToMankind];

    /// <summary>
    /// The theme name, emitted as <c>data-docs-theme</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The label shown in the theme picker.
    /// </summary>
    public string DisplayName { get; }

    private DocsTheme(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    /// <summary>
    /// Creates a reference to a consumer-defined theme.
    /// </summary>
    /// <param name="name">
    /// The theme name used in your stylesheet's
    /// <c>[data-docs-theme="..."]</c> selectors. Must be a non-empty string of
    /// lowercase letters, digits and hyphens.
    /// </param>
    /// <param name="displayName">
    /// The label shown in the theme picker. Defaults to the name with its first
    /// letter capitalised and hyphens as spaces, so <c>acme-dark</c> reads
    /// "Acme dark".
    /// </param>
    /// <exception cref="ArgumentException">
    /// The name is empty or contains characters that would not survive being
    /// written into an HTML attribute selector.
    /// </exception>
    public static DocsTheme Custom(string name, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Theme name must not be empty.", nameof(name));
        }

        if (!ThemeNamePattern().IsMatch(name))
        {
            throw new ArgumentException(
                $"Theme name '{name}' is invalid. Use lowercase letters, digits and hyphens, "
                    + "for example 'acme' or 'acme-dark'.",
                nameof(name)
            );
        }

        return new DocsTheme(
            name,
            string.IsNullOrWhiteSpace(displayName)
                ? char.ToUpperInvariant(name[0]) + name[1..].Replace('-', ' ')
                : displayName
        );
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <inheritdoc />
    public bool Equals(DocsTheme? other) =>
        other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DocsTheme);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>Compares two themes by name.</summary>
    public static bool operator ==(DocsTheme? left, DocsTheme? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compares two themes by name.</summary>
    public static bool operator !=(DocsTheme? left, DocsTheme? right) => !(left == right);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ThemeNamePattern();
}
