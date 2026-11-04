namespace Spillgebees.Blazor.Docs.Sdk;

/// <summary>
/// Provides methods for building API reference navigation pages from an <see cref="ApiManifest"/>.
/// </summary>
/// <remarks>
/// Generated API links use relative href values (for example, <c>api/Namespace.TypeName</c>). When
/// hosting docs under a subpath, configure the application base href to that subpath so API links
/// resolve correctly.
/// </remarks>
public static class ApiReferenceNav
{
    /// <summary>
    /// Converts an <see cref="ApiManifest"/> into a sorted list of <see cref="NavPage"/> entries.
    /// </summary>
    /// <remarks>
    /// The generated <see cref="NavPage.Href"/> values are relative paths under <c>api/</c>.
    /// </remarks>
    /// <param name="manifest">The API manifest to convert.</param>
    /// <returns>
    /// A read-only list of <see cref="NavPage"/> instances, one per type, ordered by title.
    /// </returns>
    public static IReadOnlyList<NavPage> FromManifest(ApiManifest manifest) =>
        manifest
            .Types.Select(t => new NavPage(t.Name, $"api/{GetRouteValue(t)}", DocSearchEntryKind.Type))
            .OrderBy(p => p.Title, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Finds a manifest type by friendly slug, stable symbol ID, or legacy full name route value.
    /// </summary>
    /// <param name="manifest">The API manifest to search.</param>
    /// <param name="routeValue">The route segment supplied by the API reference page.</param>
    /// <returns>The matching type, or <see langword="null" /> if no match exists.</returns>
    public static ApiTypeInfo? FindType(ApiManifest manifest, string routeValue)
    {
        var normalizedRouteValue = Uri.UnescapeDataString(routeValue).Trim('/');
        return manifest.Types.FirstOrDefault(type =>
            string.Equals(type.Slug, normalizedRouteValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type.Id, normalizedRouteValue, StringComparison.Ordinal)
            || string.Equals(type.FullName, normalizedRouteValue, StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Loads the API manifest for the assembly containing <typeparamref name="T"/> and finds
    /// a type by friendly slug, stable symbol ID, or legacy full name route value.
    /// </summary>
    /// <typeparam name="T">A type from the library whose API manifest should be loaded.</typeparam>
    /// <param name="routeValue">The route segment supplied by the API reference page.</param>
    /// <returns>The matching type, or <see langword="null" /> if no manifest or type match exists.</returns>
    public static ApiTypeInfo? FindType<T>(string routeValue)
    {
        var assembly = typeof(T).Assembly;
        var assemblyName = assembly.GetName().Name;

        if (assemblyName is null)
        {
            return null;
        }

        var manifest = EmbeddedResourceLocator.LoadApiManifest(assemblyName, assembly);
        return manifest is null ? null : FindType(manifest, routeValue);
    }

    /// <summary>
    /// Generates API reference navigation pages by loading the embedded <c>ApiManifest</c> resource
    /// for the assembly that contains <typeparamref name="T"/>. Searches that assembly first, then
    /// falls back to all loaded assemblies (e.g. the docs project where the resource is actually embedded).
    /// </summary>
    /// <typeparam name="T">
    /// A type from the library whose API manifest should be loaded.
    /// </typeparam>
    /// <returns>
    /// A read-only list of <see cref="NavPage"/> instances built from the manifest, or an empty list
    /// if the resource could not be found or deserialized.
    /// </returns>
    public static IReadOnlyList<NavPage> Generate<T>()
    {
        var assembly = typeof(T).Assembly;
        var assemblyName = assembly.GetName().Name;

        if (assemblyName is null)
        {
            return [];
        }

        var manifest = EmbeddedResourceLocator.LoadApiManifest(assemblyName, assembly);
        return manifest is null ? [] : FromManifest(manifest);
    }

    private static string GetRouteValue(ApiTypeInfo type)
    {
        if (!string.IsNullOrWhiteSpace(type.Slug))
        {
            return type.Slug;
        }

        if (!string.IsNullOrWhiteSpace(type.Id))
        {
            return type.Id;
        }

        return type.FullName;
    }
}
