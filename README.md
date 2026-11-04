# Spillgebees.Blazor.Docs.Sdk

[![Build & test](https://github.com/Spillgebees/Blazor.Docs.Sdk/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Spillgebees/Blazor.Docs.Sdk/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/Spillgebees.Blazor.Docs.Sdk?label=nuget)](https://www.nuget.org/packages/Spillgebees.Blazor.Docs.Sdk)

A Razor Class Library NuGet package for building and deploying documentation sites for Spillgebees Blazor component libraries. Each component repo hosts its own docs site by referencing this SDK, without a combined repo or companion PRs.

## Features

- Shell layout: top bar, sidebar, mobile drawer, sticky on-this-page rail with scroll spy, footer
- Command palette search (`Ctrl`/`Cmd` + `K`, or `/`) over pages and API types
- Three built-in themes plus a documented token contract for authoring your own
- Colour scheme preference (system / light / dark) with `localStorage` persistence
- Live component rendering alongside extracted source via `ExampleView`, with
  preview / source / split modes and 375px / 768px viewport clamps
- Auto-generated API reference from Roslyn API manifests, with member filtering,
  collapsible groups and deep-linkable anchors
- Build-time source extraction and API manifest generation via MSBuild targets
- Self-hosted fonts only, with no third-party CDN request at runtime
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
| `DocSite` | Root layout: top bar, sidebar, mobile drawer, on-this-page rail, and search. Owns theme selection |
| `ExampleView` | Renders a live component with its extracted source; preview/source/split modes (split stacks vertically with a capped code pane) and viewport clamps |
| `CodeBlock` | Syntax-highlighted snippet with a line-number gutter, a soft-wrap toggle, file tabs (a file picker past four files) and a copy button |
| `ApiDoc` | Generated API reference for a type, with member filtering, collapsible groups and deep links |
| `DocSearch` | Command palette, rendered by `DocSite`. Also usable standalone |
| `OnThisPage` | Sticky heading rail with scroll spy, plus a disclosure fallback on narrow viewports |
| `ThemeToggle` | Cycles the colour scheme preference: system → light → dark |
| `ThemePicker` | Selects the theme family. Rendered by `DocSite` when `ShowThemePicker` is set |
| `Breadcrumb` | Section / page trail |
| `DocFooter` | Attribution, licence and repository link. Rendered by `DocSite` at the bottom of the sidebar |
| `DocIcon` | The shared inline icon set |

## Upgrading from 0.8

The C# API is compatible apart from `DocSidebar`, which was removed: the sidebar is now part
of `DocSite`, so that the drawer, backdrop and scroll lock stay in one place. `NavPage`,
`NavSection` and `OnThisPageSection` gained optional positional parameters, so code that
deconstructs them or was compiled against 0.8 needs a rebuild. The stylesheet
was rewritten, so most of the work is in markup and CSS:

- Remove `<OnThisPage />` from your pages. `DocSite` renders it now, as a rail on wide
  screens and a disclosure above the content on narrow ones. A page-level copy shows up as a
  second rail inside the content.
- Drop `// ` prefixes from headings. They are no longer stripped from the on-this-page list,
  and themes that want heading markers add them with CSS. To show a different title in the
  list, set `data-docs-toc-title` on the heading.
- The `doc-*` classes are gone, including `doc-hero` and `doc-card`. Build landing pages from
  `docs-eyebrow`, `docs-title`, `docs-lede`, `docs-meta-row` with `docs-chip`, and
  `docs-callout`; the demo's `Home.razor` shows them together.
- Rename CSS variables in your own styles:

| Before | After |
|--------|-------|
| `--color-text` | `--docs-text` |
| `--color-text-secondary` | `--docs-text-2` |
| `--color-text-muted` | `--docs-text-3` |
| `--color-text-dim` | `--docs-text-4` |
| `--color-surface` | `--docs-surface-1` |
| `--color-surface-raised` | `--docs-surface-2` |
| `--color-surface-overlay` | `--docs-surface-3` |
| `--color-surface-sidebar`, `--color-surface-topbar` | `--docs-bg` |
| `--color-border` | `--docs-line` |
| `--color-primary` | `--docs-accent` |
| `--font-sans` | `--docs-font-sans` |
| `--font-mono` | `--docs-font-mono` |

Undefined variables drop the declarations that use them without any error, so check the
pages in a browser rather than relying on a clean build.

## Theming

Two axes, applied as attributes on `<html>`:

| Attribute | Meaning |
|-----------|---------|
| `data-docs-theme` | The palette and type family: `refined` (default), `terminal` or `glory-to-mankind` |
| `data-docs-scheme` | The resolved colour scheme: `light` or `dark` |

The `glory-to-mankind` palette builds on
[SunkenInTime's NieR: Automata theme for T3 Code](https://github.com/SunkenInTime/t3-themes).

Pick the theme on `DocSite`, and optionally let visitors switch:

```razor
<DocSite ProjectName="Your.Library"
         LogoEmoji="🍓"
         GitHubUrl="https://github.com/Spillgebees/Your.Library"
         Navigation="@Navigation"
         Theme="@DocsTheme.Terminal"
         ShowThemePicker="true">
    @Body
</DocSite>
```

The colour scheme is a separate, visitor-controlled preference (`system` by default,
following the OS and updating live). Both are persisted to `localStorage`. Until the SDK's
script runs, the stylesheet follows the OS colour scheme, so a light system never flashes
dark. A stored theme family is applied when `DocSite` first renders.

A stored theme is only honoured when it is in `AvailableThemes`, since every docs site on
the same origin shares the preference.

### Custom themes

Every rule in the stylesheet reads from CSS custom properties and contains no literal
colour, font, radius or shadow value. A theme is therefore just a block redefining
those tokens.

1. Copy `_content/Spillgebees.Blazor.Docs.Sdk/docs-sdk-theme-template.css` into your
   docs project and rename the theme in its selectors.
2. Reference it *after* the SDK stylesheet in `index.html`.
3. Select it with `Theme="@DocsTheme.Custom("acme")"`.

You only need to define the tokens you want to change; anything omitted falls back to
`refined`.

**One rule to get right:** accent steps are named for *contrast*, not lightness.
`--docs-accent-strong` must have **more** contrast against `--docs-bg` than
`--docs-accent`: lighter on a dark scheme, and **darker** on a light one. It is used
for links, active navigation, inline code and required badges, so getting this backwards
silently drops all of them below 4.5:1 in light mode.

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
