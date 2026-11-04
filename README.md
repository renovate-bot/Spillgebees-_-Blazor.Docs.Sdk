# Spillgebees.Blazor.Docs.Sdk

[![Build & test](https://github.com/Spillgebees/Blazor.Docs.Sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Spillgebees/Blazor.Docs.Sdk/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Spillgebees.Blazor.Docs.Sdk?label=nuget)](https://www.nuget.org/packages/Spillgebees.Blazor.Docs.Sdk)

A Razor Class Library NuGet package for building and deploying documentation sites for Spillgebees Blazor component libraries. Each component repo hosts its own docs site by referencing this SDK, without a combined repo or companion PRs.

## Features

- Shell layout with collapsible sidebar, top bar, and footer
- Dark/light theme toggle with `localStorage` persistence
- Live component rendering alongside syntax-highlighted source code via `ExampleView`
- Auto-generated API reference pages from Roslyn API manifests, decorated with XML doc comments
- Build-time source extraction and API manifest generation via MSBuild targets (runs automatically for direct package consumers)
- Sticky on-page navigation and sidebar links that wrap long component/type names
- Self-hosted Inter Variable and JetBrains Mono fonts
- Reusable GitHub Actions workflow for deploying to GitHub Pages

## Package Sources

This package is published to [nuget.org](https://www.nuget.org/packages/Spillgebees.Blazor.Docs.Sdk). No special feed configuration is needed.

## Quick Start

### 1. Install the NuGet package

```xml
<PackageReference Include="Spillgebees.Blazor.Docs.Sdk" />
```

### 2. Add ProjectReference items

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Spillgebees.Blazor.Docs.Sdk" />
    <ProjectReference Include="../YourLibrary/YourLibrary.csproj" DocsApi="true" />
    <ProjectReference Include="../YourLibrary.Samples.Shared/YourLibrary.Samples.Shared.csproj" />
  </ItemGroup>
</Project>
```

Use `DocsApi="true"` only on the library whose public API should appear in the API reference. Other project references, such as shared samples, remain normal references and are not scanned for API manifests.

### 3. Create `MainLayout.razor` with `DocSite`

```razor
@inherits LayoutComponentBase

<DocSite ProjectName="Your.Library"
         LogoEmoji="🍓"
         GitHubUrl="https://github.com/Spillgebees/Your.Library"
         Navigation="@Navigation">
    @Body
</DocSite>

@code {
    private static readonly NavSection[] Navigation =
    [
        new("Getting Started", [
            new NavPage("Home", ""),
            new NavPage("Installation", "installation"),
        ]),
        new("Components", [
            new NavPage("MyComponent", "components/my-component"),
        ]),
        new("API Reference", ApiReferenceNav.Generate<MyComponent>()),
    ];
}
```

### 4. Add the CSS link to `wwwroot/index.html`

```html
<link rel="stylesheet" href="_content/Spillgebees.Blazor.Docs.Sdk/docs-sdk.css" />
```

### 5. Create pages with `@page` directives

```razor
@page "/installation"

<h1>Installation</h1>
<p>Add the NuGet package to your project.</p>
```

## Components

| Component | Description |
|-----------|-------------|
| `DocSite` | Root layout component wrapping the entire app with sidebar, top bar, and footer |
| `ExampleView` | Renders a live Blazor component alongside its build-time-extracted, syntax-highlighted source code |
| `CodeBlock` | Displays a syntax-highlighted code snippet with a copy-to-clipboard button |
| `ApiDoc` | Auto-generated API documentation page for a type, sourced from XML doc comments and reflection |
| `ThemeToggle` | Switches between dark and light mode and persists the preference to `localStorage` |
| `DocSidebar` | Collapsible navigation sidebar driven by the `Navigation` parameter on `DocSite` |
| `DocFooter` | Footer displaying the GitHub link configured on `DocSite` |

## Build-Time Automation

When your docs project directly references the SDK package, MSBuild targets run automatically during build:

**Source extraction** scans your Razor files for `ExampleView<TComponent>` references, locates the matching `.razor` and `.razor.cs` source files in referenced projects, and embeds them as assembly resources. `ExampleView` reads these at runtime to display syntax-highlighted source alongside live components.

**API manifest generation** runs a Roslyn build-time tool for `ProjectReference` items that opt in with `DocsApi="true"`. The tool reads the assembly the referenced project already built, so the project isn't built a second time. The generated manifest is based on public CLR symbols, so internal documented types are excluded and property, method, generic, nullable, enum, and Blazor parameter signatures are populated accurately. Summaries, remarks, and parameter descriptions come from the project's XML documentation file, so set `GenerateDocumentationFile` to `true` in that project. Without it the build warns with `DOCSSDK001` and the API reference has no summaries. Turning it on also reports `CS1591` for undocumented public members, which you can add to `NoWarn` if the library isn't fully documented. The tool only runs again when the referenced assembly, its XML documentation file, or the docs project's references change (and on every build while the XML documentation file is missing), and it leaves the manifest untouched when its content is the same, so a build with no changes doesn't recompile the docs project.

API navigation uses friendly route slugs such as `api/MyComponent` or `api/TrackedEntityLayer-1` for generic types. Legacy full-name route values still resolve when routed pages use the SDK resolver.

At runtime, `ExampleView`, `ApiDoc`, and `ApiReferenceNav.Generate<T>()` search for embedded resources across all loaded assemblies — the library assembly is checked first, then the docs project assembly and others. This means the resources work regardless of which assembly they end up in.

The targets only run in projects that directly reference the SDK (packed into `build/`, not `buildTransitive/`), so transitive consumers are unaffected. Generated API manifest files are written under the docs project `obj/<Configuration>/<TargetFramework>/DocsSdk/manifests/` and embedded into the docs app only; they are not added to the documented library package.

## Running the Demo

```bash
dotnet run --project samples/Spillgebees.Blazor.Docs.Sdk.Demo/
```

## Deployment

The SDK provides a reusable GitHub Actions workflow at `.github/workflows/deploy-docs.yml` for deploying to GitHub Pages. Consuming repos reference it as follows:

```yaml
# .github/workflows/deploy-docs.yml (in the consuming repo)
name: Deploy Docs

on:
  push:
    branches: [main]

jobs:
  deploy:
    uses: Spillgebees/Blazor.Docs.Sdk/.github/workflows/deploy-docs.yml@main
    with:
      docs-project-path: src/Docs/Docs.csproj
      base-href: /Your.Library/
```

The workflow publishes the WASM app, patches the `<base href>` for the GitHub Pages subpath, and deploys to `spillgebees.github.io/{repo-name}/`.

## License

MIT
