namespace Spillgebees.Blazor.Docs.Sdk.ApiManifestTool;

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spillgebees.Blazor.Docs.Sdk;

internal static partial class Program
{
    private const string ParameterAttributeName = "Microsoft.AspNetCore.Components.ParameterAttribute";
    private const string CascadingParameterAttributeName =
        "Microsoft.AspNetCore.Components.CascadingParameterAttribute";

    private static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    private static readonly SymbolDisplayFormat ConstraintDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );

    public static int Main(string[] args)
    {
        var options = ToolOptions.Parse(args);
        if (options is null)
        {
            Console.Error.WriteLine(
                "Usage: --assembly <assembly.dll> --references <file> --output <manifest.json> [--documentation <assembly.xml>]"
            );
            return 2;
        }

        try
        {
            var documentation = File.Exists(options.DocumentationPath)
                ? new XmlDocumentationFile(options.DocumentationPath!)
                : null;
            var assemblyReference = MetadataReference.CreateFromFile(
                options.AssemblyPath,
                documentation: documentation
            );
            var assemblyFileName = Path.GetFileName(options.AssemblyPath);

            // the docs project's references resolve the assembly's base types and attributes
            var references = File.ReadAllLines(options.ReferencesPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !string.Equals(Path.GetFileName(x), assemblyFileName, StringComparison.OrdinalIgnoreCase))
                .Select(x => MetadataReference.CreateFromFile(x))
                .Prepend(assemblyReference);

            var compilation = CSharpCompilation.Create("DocsSdk.ApiManifest", references: references);
            if (compilation.GetAssemblyOrModuleSymbol(assemblyReference) is not IAssemblySymbol assembly)
            {
                Console.Error.WriteLine($"Could not load '{options.AssemblyPath}'.");
                return 1;
            }

            var manifest = Generate(assembly);
            var json = JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                new JsonSerializerOptions { WriteIndented = true }
            );

            // rewriting an unchanged manifest would make the docs project recompile
            if (!File.Exists(options.OutputPath) || !File.ReadAllBytes(options.OutputPath).AsSpan().SequenceEqual(json))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
                File.WriteAllBytes(options.OutputPath, json);
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static ApiManifest Generate(IAssemblySymbol assembly)
    {
        var manifest = new ApiManifest { AssemblyName = assembly.Identity.Name };
        var types = EnumerateNamedTypes(assembly.GlobalNamespace)
            .Where(IsDocumentablePublicType)
            .OrderBy(x => GetSymbolId(x), StringComparer.Ordinal)
            .ToList();

        var slugCounts = types
            .GroupBy(CreateBaseSlug, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var slugsByDocId = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in types)
        {
            var docId = DocumentationCommentId.CreateDeclarationId(type);
            if (!string.IsNullOrWhiteSpace(docId))
            {
                slugsByDocId[docId] = CreateUniqueSlug(type, slugCounts, usedSlugs);
            }
        }

        string? ResolveCref(string cref)
        {
            if (slugsByDocId.TryGetValue(cref, out var slug))
            {
                return "api/" + slug;
            }

            var typeCref = GetContainingTypeCref(cref);
            return typeCref is not null && slugsByDocId.TryGetValue(typeCref, out slug) ? "api/" + slug : null;
        }

        foreach (var type in types)
        {
            // C# 14 extension members compile to static implementation methods on the containing class
            var extensionImplementations = type.GetTypeMembers()
                .Where(x => x.IsExtension)
                .SelectMany(x => x.GetMembers())
                .OfType<IMethodSymbol>()
                .Select(x => x.AssociatedExtensionImplementation)
                .OfType<IMethodSymbol>()
                .ToHashSet(SymbolEqualityComparer.Default);

            var typeDocs = Documentation.From(type, ResolveCref);
            var typeDocId = DocumentationCommentId.CreateDeclarationId(type);
            var typeInfo = new ApiTypeInfo
            {
                Id = GetSymbolId(type),
                Slug =
                    typeDocId is not null && slugsByDocId.TryGetValue(typeDocId, out var slug)
                        ? slug
                        : CreateBaseSlug(type),
                Name = FormatDisplayName(type),
                FullName = GetSymbolId(type),
                Namespace = type.ContainingNamespace.IsGlobalNamespace
                    ? string.Empty
                    : type.ContainingNamespace.ToDisplayString(),
                Kind = GetTypeKind(type),
                Summary = typeDocs.Summary,
                SummaryHtml = typeDocs.SummaryHtml,
                Remarks = typeDocs.Remarks,
                RemarksHtml = typeDocs.RemarksHtml,
                BaseType = GetBaseType(type),
                Interfaces = type.Interfaces.Select(x => x.ToDisplayString(TypeDisplayFormat)).ToList(),
                GenericParameters = type.TypeParameters.Select(CreateGenericParameter).ToList(),
            };

            if (type.TypeKind == TypeKind.Enum)
            {
                typeInfo.EnumValues = CreateEnumValues(type, ResolveCref);
            }
            else
            {
                typeInfo.Properties = type.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(IsDocumentableProperty)
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => CreateProperty(x, ResolveCref))
                    .ToList();

                typeInfo.Methods = type.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(IsDocumentableMethod)
                    .Where(x => !extensionImplementations.Contains(x))
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .ThenBy(x => x.Parameters.Length)
                    .ThenBy(x => string.Join(",", x.Parameters.Select(p => p.Type.ToDisplayString(TypeDisplayFormat))))
                    .Select(x => CreateMethod(x, ResolveCref))
                    .ToList();

                typeInfo.Events = type.GetMembers()
                    .OfType<IEventSymbol>()
                    .Where(IsDocumentableEvent)
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => CreateEvent(x, ResolveCref))
                    .ToList();
            }

            manifest.Types.Add(typeInfo);
        }

        return manifest;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceOrTypeSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            if (member is INamespaceOrTypeSymbol namespaceOrType)
            {
                if (namespaceOrType is INamedTypeSymbol namedType)
                {
                    yield return namedType;
                }

                foreach (var nested in EnumerateNamedTypes(namespaceOrType))
                {
                    yield return nested;
                }
            }
        }
    }

    private static bool IsDocumentablePublicType(INamedTypeSymbol symbol)
    {
        if (
            symbol.DeclaredAccessibility != Accessibility.Public
            || !symbol.CanBeReferencedByName
            || IsCompilerGenerated(symbol)
            || string.Equals(symbol.Name, "_Imports", StringComparison.Ordinal)
        )
        {
            return false;
        }

        for (
            var containingType = symbol.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType
        )
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDocumentableProperty(IPropertySymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Public && !symbol.IsIndexer && !IsCompilerGenerated(symbol);

    private static bool IsDocumentableMethod(IMethodSymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Public
        && symbol.MethodKind == MethodKind.Ordinary
        && symbol.CanBeReferencedByName
        && !IsCompilerGenerated(symbol);

    private static bool IsDocumentableEvent(IEventSymbol symbol) =>
        symbol.DeclaredAccessibility == Accessibility.Public && !IsCompilerGenerated(symbol);

    // members the compiler synthesizes, such as record equality and ToString, are not part of the documented API
    private static bool IsCompilerGenerated(ISymbol symbol) =>
        HasAttribute(symbol, "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    private static ApiGenericParameter CreateGenericParameter(ITypeParameterSymbol symbol)
    {
        var constraints = new List<string>();

        if (symbol.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        if (symbol.HasReferenceTypeConstraint)
        {
            constraints.Add(
                symbol.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated ? "class?" : "class"
            );
        }

        if (symbol.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }

        constraints.AddRange(symbol.ConstraintTypes.Select(x => x.ToDisplayString(ConstraintDisplayFormat)));

        if (symbol.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }

        if (symbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return new ApiGenericParameter { Name = symbol.Name, Constraints = constraints };
    }

    private static ApiPropertyInfo CreateProperty(IPropertySymbol symbol, Func<string, string?> resolveCref)
    {
        var docs = Documentation.From(symbol, resolveCref);
        return new ApiPropertyInfo
        {
            Name = symbol.Name,
            Type = symbol.Type.ToDisplayString(TypeDisplayFormat),
            Summary = docs.Summary,
            SummaryHtml = docs.SummaryHtml,
            IsParameter = HasAttribute(symbol, ParameterAttributeName),
            IsCascadingParameter = HasAttribute(symbol, CascadingParameterAttributeName),
        };
    }

    private static ApiMethodInfo CreateMethod(IMethodSymbol symbol, Func<string, string?> resolveCref)
    {
        var docs = Documentation.From(symbol, resolveCref);
        return new ApiMethodInfo
        {
            Name = symbol.Name,
            ReturnType = symbol.ReturnType.ToDisplayString(TypeDisplayFormat),
            Summary = docs.Summary,
            SummaryHtml = docs.SummaryHtml,
            GenericParameters = symbol.TypeParameters.Select(CreateGenericParameter).ToList(),
            Parameters = symbol.Parameters.Select(parameter => CreateParameter(parameter, docs)).ToList(),
        };
    }

    private static ApiParameterInfo CreateParameter(IParameterSymbol symbol, Documentation methodDocs)
    {
        methodDocs.Parameters.TryGetValue(symbol.Name, out var doc);
        return new ApiParameterInfo
        {
            Name = symbol.Name,
            Type = symbol.Type.ToDisplayString(TypeDisplayFormat),
            Summary = doc?.Text,
            SummaryHtml = doc?.Html,
            IsOptional = symbol.IsOptional,
            DefaultValue = symbol.HasExplicitDefaultValue ? FormatDefaultValue(symbol.ExplicitDefaultValue) : null,
        };
    }

    private static ApiEventInfo CreateEvent(IEventSymbol symbol, Func<string, string?> resolveCref)
    {
        var docs = Documentation.From(symbol, resolveCref);
        return new ApiEventInfo
        {
            Name = symbol.Name,
            Type = symbol.Type.ToDisplayString(TypeDisplayFormat),
            Summary = docs.Summary,
            SummaryHtml = docs.SummaryHtml,
        };
    }

    private static List<ApiEnumValue> CreateEnumValues(INamedTypeSymbol type, Func<string, string?> resolveCref) =>
        type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(x => x.DeclaredAccessibility == Accessibility.Public && x.HasConstantValue)
            .OrderBy(x => x.MetadataName, StringComparer.Ordinal)
            .Select(x =>
            {
                var docs = Documentation.From(x, resolveCref);
                return new ApiEnumValue
                {
                    Name = x.Name,
                    Value = Convert.ToInt64(x.ConstantValue, CultureInfo.InvariantCulture),
                    Summary = docs.Summary,
                    SummaryHtml = docs.SummaryHtml,
                };
            })
            .ToList();

    private static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol
            .GetAttributes()
            .Any(attribute =>
                string.Equals(GetSymbolId(attribute.AttributeClass), metadataName, StringComparison.Ordinal)
            );

    private static string GetTypeKind(INamedTypeSymbol symbol)
    {
        if (IsBlazorComponent(symbol))
        {
            return "component";
        }

        return symbol.TypeKind switch
        {
            TypeKind.Enum => "enum",
            TypeKind.Interface => "interface",
            TypeKind.Struct => "struct",
            TypeKind.Delegate => "delegate",
            _ when symbol.IsRecord => "record",
            _ => "class",
        };
    }

    private static bool IsBlazorComponent(INamedTypeSymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (
                string.Equals(
                    GetSymbolId(current),
                    "Microsoft.AspNetCore.Components.ComponentBase",
                    StringComparison.Ordinal
                )
            )
            {
                return true;
            }
        }

        return symbol.AllInterfaces.Any(x =>
            string.Equals(GetSymbolId(x), "Microsoft.AspNetCore.Components.IComponent", StringComparison.Ordinal)
        );
    }

    private static string? GetBaseType(INamedTypeSymbol symbol) =>
        symbol.BaseType is null || symbol.BaseType.SpecialType == SpecialType.System_Object
            ? null
            : symbol.BaseType.ToDisplayString(TypeDisplayFormat);

    private static string FormatDisplayName(INamedTypeSymbol symbol)
    {
        var name = symbol.Name;
        if (symbol.TypeParameters.Length == 0)
        {
            return name;
        }

        return name + "<" + string.Join(", ", symbol.TypeParameters.Select(x => x.Name)) + ">";
    }

    private static string GetSymbolId(INamedTypeSymbol? symbol)
    {
        if (symbol is null)
        {
            return string.Empty;
        }

        var stack = new Stack<string>();
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            stack.Push(current.MetadataName);
        }

        var containingName = string.Join(".", stack);
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? containingName
            : symbol.ContainingNamespace.ToDisplayString() + "." + containingName;
    }

    private static string CreateBaseSlug(INamedTypeSymbol symbol)
    {
        var slug = SlugUnsafeCharacters().Replace(symbol.Name, "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "type";
        }

        return symbol.TypeParameters.Length == 0 ? slug : slug + "-" + symbol.TypeParameters.Length;
    }

    private static string CreateUniqueSlug(
        INamedTypeSymbol symbol,
        Dictionary<string, int> slugCounts,
        HashSet<string> usedSlugs
    )
    {
        var baseSlug = CreateBaseSlug(symbol);
        var slug = baseSlug;

        if (slugCounts.TryGetValue(baseSlug, out var count) && count > 1)
        {
            var namespaceSuffix = SlugUnsafeCharacters()
                .Replace(symbol.ContainingNamespace.ToDisplayString(), "-")
                .Trim('-');
            slug = string.IsNullOrEmpty(namespaceSuffix) ? baseSlug : baseSlug + "-" + namespaceSuffix;
        }

        var uniqueSlug = slug;
        var index = 2;
        while (!usedSlugs.Add(uniqueSlug))
        {
            uniqueSlug = slug + "-" + index.ToString(CultureInfo.InvariantCulture);
            index++;
        }

        return uniqueSlug;
    }

    private static string FormatDefaultValue(object? value) =>
        value switch
        {
            null => "null",
            string text => "\""
                + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
                + "\"",
            char character => "'"
                + character
                    .ToString()
                    .Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("'", "\\'", StringComparison.Ordinal)
                + "'",
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

    private static string? GetContainingTypeCref(string cref)
    {
        if (cref.Length < 3 || cref[1] != ':' || cref[0] == 'T')
        {
            return null;
        }

        var memberId = cref[2..];
        var parametersIndex = memberId.IndexOf('(', StringComparison.Ordinal);
        if (parametersIndex >= 0)
        {
            memberId = memberId[..parametersIndex];
        }

        var lastDotIndex = memberId.LastIndexOf('.');
        return lastDotIndex <= 0 ? null : "T:" + memberId[..lastDotIndex];
    }

    [GeneratedRegex(@"[^A-Za-z0-9]+")]
    private static partial Regex SlugUnsafeCharacters();

    private sealed record ToolOptions(
        string AssemblyPath,
        string? DocumentationPath,
        string ReferencesPath,
        string OutputPath
    )
    {
        public static ToolOptions? Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index += 2)
            {
                if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                {
                    return null;
                }

                values[args[index][2..]] = args[index + 1];
            }

            return
                values.TryGetValue("assembly", out var assemblyPath)
                && values.TryGetValue("references", out var referencesPath)
                && values.TryGetValue("output", out var outputPath)
                ? new ToolOptions(
                    Path.GetFullPath(assemblyPath),
                    values.TryGetValue("documentation", out var documentationPath)
                        ? Path.GetFullPath(documentationPath)
                        : null,
                    Path.GetFullPath(referencesPath),
                    Path.GetFullPath(outputPath)
                )
                : null;
        }
    }

    private sealed partial record Documentation(
        string? Summary,
        string? SummaryHtml,
        string? Remarks,
        string? RemarksHtml,
        IReadOnlyDictionary<string, DocumentationText> Parameters
    )
    {
        public static Documentation From(ISymbol symbol, Func<string, string?> resolveCref)
        {
            var xml = symbol.GetDocumentationCommentXml(expandIncludes: true);
            if (string.IsNullOrWhiteSpace(xml))
            {
                return Empty;
            }

            try
            {
                var element = XElement.Parse(xml);
                var parameters = element
                    .Elements("param")
                    .Select(x => new
                    {
                        Name = x.Attribute("name")?.Value,
                        Documentation = CreateDocumentationText(x, resolveCref),
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.Documentation.Text))
                    .ToDictionary(x => x.Name!, x => x.Documentation, StringComparer.Ordinal);

                var summary = CreateDocumentationText(element.Element("summary"), resolveCref);
                var remarks = CreateDocumentationText(element.Element("remarks"), resolveCref);

                return new Documentation(summary.Text, summary.Html, remarks.Text, remarks.Html, parameters);
            }
            catch
            {
                return Empty;
            }
        }

        private static Documentation Empty { get; } =
            new(null, null, null, null, new Dictionary<string, DocumentationText>());

        private static DocumentationText CreateDocumentationText(XElement? element, Func<string, string?> resolveCref)
        {
            if (element is null)
            {
                return new DocumentationText(null, null);
            }

            return new DocumentationText(
                Normalize(element.Value),
                NormalizeHtml(RenderNodes(element.Nodes(), resolveCref))
            );
        }

        private static string RenderNodes(IEnumerable<XNode> nodes, Func<string, string?> resolveCref) =>
            string.Concat(nodes.Select(node => RenderNode(node, resolveCref)));

        private static string RenderNode(XNode node, Func<string, string?> resolveCref) =>
            node switch
            {
                XText text => HtmlEncoder.Default.Encode(text.Value),
                XElement element => RenderElement(element, resolveCref),
                _ => string.Empty,
            };

        private static string RenderElement(XElement element, Func<string, string?> resolveCref)
        {
            var children = RenderNodes(element.Nodes(), resolveCref);
            return element.Name.LocalName switch
            {
                "c" => "<code>" + HtmlEncoder.Default.Encode(element.Value) + "</code>",
                "code" => "<pre><code>" + HtmlEncoder.Default.Encode(element.Value.Trim()) + "</code></pre>",
                "example" => """<div class="api-doc-example">""" + children + "</div>",
                "inheritdoc" => string.Empty,
                "list" => RenderList(element, resolveCref),
                "para" => "<p>" + children + "</p>",
                "paramref" or "typeparamref" => RenderNamedCode(element),
                "see" or "seealso" => RenderReference(element, children, resolveCref),
                "br" => "<br />",
                "term" => "<strong>" + children + "</strong>",
                "description" => children,
                _ => children,
            };
        }

        private static string RenderList(XElement element, Func<string, string?> resolveCref)
        {
            var type = element.Attribute("type")?.Value;
            if (string.Equals(type, "table", StringComparison.OrdinalIgnoreCase))
            {
                var rows = element
                    .Elements("item")
                    .Select(item =>
                    {
                        var term = RenderNodes(item.Elements("term").SelectMany(x => x.Nodes()), resolveCref);
                        var description = RenderNodes(
                            item.Elements("description").SelectMany(x => x.Nodes()),
                            resolveCref
                        );
                        return "<tr><td>" + term + "</td><td>" + description + "</td></tr>";
                    });
                return """<table class="api-doc-xml-table"><tbody>""" + string.Concat(rows) + "</tbody></table>";
            }

            var tag = string.Equals(type, "number", StringComparison.OrdinalIgnoreCase) ? "ol" : "ul";
            var items = element
                .Elements("item")
                .Select(item =>
                {
                    var term = RenderNodes(item.Elements("term").SelectMany(x => x.Nodes()), resolveCref);
                    var description = RenderNodes(item.Elements("description").SelectMany(x => x.Nodes()), resolveCref);
                    var content = string.IsNullOrWhiteSpace(term) ? description : term + " " + description;
                    return "<li>" + content + "</li>";
                });

            return "<" + tag + ">" + string.Concat(items) + "</" + tag + ">";
        }

        private static string RenderNamedCode(XElement element)
        {
            var name = element.Attribute("name")?.Value;
            return string.IsNullOrWhiteSpace(name)
                ? string.Empty
                : "<code>" + HtmlEncoder.Default.Encode(name) + "</code>";
        }

        private static string RenderReference(XElement element, string children, Func<string, string?> resolveCref)
        {
            var href = element.Attribute("href")?.Value;
            if (!string.IsNullOrWhiteSpace(href) && IsSafeHref(href))
            {
                var text = string.IsNullOrWhiteSpace(children) ? HtmlEncoder.Default.Encode(href) : children;
                return "<a href=\""
                    + HtmlEncoder.Default.Encode(href)
                    + "\" target=\"_blank\" rel=\"noopener noreferrer\">"
                    + text
                    + "</a>";
            }

            var langword = element.Attribute("langword")?.Value;
            if (!string.IsNullOrWhiteSpace(langword))
            {
                return "<code>" + HtmlEncoder.Default.Encode(langword) + "</code>";
            }

            var cref = element.Attribute("cref")?.Value;
            if (string.IsNullOrWhiteSpace(cref))
            {
                return children;
            }

            var label = string.IsNullOrWhiteSpace(children)
                ? HtmlEncoder.Default.Encode(FormatCrefLabel(cref))
                : children;
            var link = resolveCref(cref);
            return link is null
                ? "<code>" + label + "</code>"
                : "<a href=\"" + HtmlEncoder.Default.Encode(link) + "\"><code>" + label + "</code></a>";
        }

        private static bool IsSafeHref(string href)
        {
            if (href.StartsWith('#'))
            {
                return true;
            }

            return Uri.TryCreate(href, UriKind.Absolute, out var uri)
                && (
                    uri.Scheme == Uri.UriSchemeHttp
                    || uri.Scheme == Uri.UriSchemeHttps
                    || uri.Scheme == Uri.UriSchemeMailto
                );
        }

        private static string FormatCrefLabel(string cref)
        {
            var value = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
            var parametersIndex = value.IndexOf('(', StringComparison.Ordinal);
            if (parametersIndex >= 0)
            {
                value = value[..parametersIndex];
            }

            var lastDotIndex = value.LastIndexOf('.');
            return lastDotIndex < 0 ? value : value[(lastDotIndex + 1)..];
        }

        private static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Whitespace().Replace(value.Trim(), " ");
        }

        private static string? NormalizeHtml(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Whitespace().Replace(value.Trim(), " ");
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();
    }

    private sealed record DocumentationText(string? Text, string? Html);

    private sealed class XmlDocumentationFile(string path) : DocumentationProvider
    {
        // keeps each member's XML exactly as the compiler wrote it
        private readonly Dictionary<string, string> _members = XDocument
            .Load(path, LoadOptions.PreserveWhitespace)
            .Descendants("member")
            .Select(x => (Name: x.Attribute("name")?.Value, Xml: x.ToString(SaveOptions.DisableFormatting)))
            .Where(x => x.Name is not null)
            .DistinctBy(x => x.Name)
            .ToDictionary(x => x.Name!, x => x.Xml, StringComparer.Ordinal);

        protected override string? GetDocumentationForSymbol(
            string documentationMemberID,
            CultureInfo preferredCulture,
            CancellationToken cancellationToken = default
        ) => _members.GetValueOrDefault(documentationMemberID);

        public override bool Equals(object? obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => path.GetHashCode(StringComparison.Ordinal);
    }
}
