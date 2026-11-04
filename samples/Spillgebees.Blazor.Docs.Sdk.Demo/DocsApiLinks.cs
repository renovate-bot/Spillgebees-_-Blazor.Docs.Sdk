using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Demo;

/// <summary>
/// Resolves API reference links from the embedded manifest instead of
/// hard-coding them.
/// </summary>
/// <remarks>
/// Hard-coded <c>api/Foo</c> links silently rot: nothing fails at build time and
/// the page 404s at runtime. Resolving against the manifest means a link is
/// rendered only when the target actually exists.
/// </remarks>
internal static class DocsApiLinks
{
    private static readonly Lazy<IReadOnlyList<NavPage>> Pages = new(() => ApiReferenceNav.Generate<DocSite>());

    /// <summary>
    /// The API reference href for <paramref name="typeName"/>, or
    /// <see langword="null"/> when that type is not in the manifest.
    /// </summary>
    public static string? For(string typeName) =>
        Pages
            .Value.FirstOrDefault(page =>
                string.Equals(page.Title, typeName, StringComparison.Ordinal)
                || page.Title.StartsWith(typeName + "<", StringComparison.Ordinal)
            )
            ?.Href;
}
