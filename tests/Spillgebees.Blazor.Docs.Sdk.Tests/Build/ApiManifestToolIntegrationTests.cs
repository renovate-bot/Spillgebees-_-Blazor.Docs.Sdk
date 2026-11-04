using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Build;

[NotInParallel("ApiManifestToolBuild")]
public class ApiManifestToolIntegrationTests
{
    [Test]
    public async Task Should_generate_manifest_only_for_docs_api_project_reference()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            // act
            await ExecuteProcessAsync(
                "dotnet",
                $"msbuild \"{fixture.DocsProjectPath}\" -nologo -restore -t:DocsSdk_GenerateApiManifest",
                TimeSpan.FromSeconds(60)
            );

            // assert
            File.Exists(fixture.ManifestPath).Should().BeTrue();
            File.Exists(Path.Combine(Path.GetDirectoryName(fixture.ManifestPath)!, "UnoptedLibrary.json"))
                .Should()
                .BeFalse();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_generate_roslyn_manifest_with_public_surface_and_signatures()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            // act
            await ExecuteProcessAsync(
                "dotnet",
                $"msbuild \"{fixture.DocsProjectPath}\" -nologo -restore -t:DocsSdk_GenerateApiManifest",
                TimeSpan.FromSeconds(60)
            );

            var manifest = await ReadManifestAsync(fixture);

            // assert
            manifest.Types.Select(x => x.Id).Should().NotContain("DocumentedLibrary.InternalHelper");
            manifest.Types.Select(x => x.Name).Should().NotContain("_Imports");

            var component = manifest.Types.Single(x => x.Id == "DocumentedLibrary.TrackedEntityLayer`1");
            component.Name.Should().Be("TrackedEntityLayer<TItem>");
            component.Slug.Should().Be("TrackedEntityLayer-1");
            component.Kind.Should().Be("component");
            component
                .SummaryHtml.Should()
                .Contain(
                    """<a href="https://example.com/map" target="_blank" rel="noopener noreferrer">map docs</a>"""
                );
            component.SummaryHtml.Should().Contain("""<a href="api/MapMode"><code>MapMode</code></a>""");
            component.SummaryHtml.Should().Contain("<ul><li>Supports <code>tracked items</code>.</li></ul>");
            component
                .RemarksHtml.Should()
                .Contain("<pre><code>var layer = new TrackedEntityLayer&lt;string&gt;();</code></pre>");
            component.GenericParameters.Single().Constraints.Should().Contain("class");

            component
                .Properties.Single(x => x.Name == "Items")
                .Type.Should()
                .Be("System.Collections.Generic.IReadOnlyList<TItem>?");
            component.Properties.Single(x => x.Name == "Items").IsParameter.Should().BeTrue();
            component.Properties.Single(x => x.Name == "Theme").IsCascadingParameter.Should().BeTrue();

            var method = component.Methods.Single(x => x.Name == "Find" && x.Parameters.Count == 2);
            method.ReturnType.Should().Be("TItem?");
            method.Parameters[0].Name.Should().Be("id");
            method.Parameters[0].Type.Should().Be("string");
            method.Parameters[0].Summary.Should().Be("Entity id.");
            method.Parameters[0].SummaryHtml.Should().Be("Entity id.");
            method.Parameters[1].Name.Should().Be("includeInactive");
            method.Parameters[1].Type.Should().Be("bool");
            method.Parameters[1].IsOptional.Should().BeTrue();
            method.Parameters[1].DefaultValue.Should().Be("false");

            component.Methods.Count(x => x.Name == "Find").Should().Be(2);

            var enumType = manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapMode");
            enumType.EnumValues.Single(x => x.Name == "Hybrid").Value.Should().Be(42);

            var razorComponent = manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapLegend");
            razorComponent.Kind.Should().Be("component");
            razorComponent.Properties.Single(x => x.Name == "Title").IsParameter.Should().BeTrue();
            razorComponent.Properties.Single(x => x.Name == "Title").Summary.Should().Be("Legend title.");

            var record = manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapPoint");
            record.Kind.Should().Be("record");
            record.Summary.Should().Be("A point on the map.");
            record.Properties.Select(x => x.Name).Should().BeEquivalentTo(["Latitude", "Longitude"]);
            record.Methods.Should().BeEmpty();

            manifest.Types.Should().NotContain(x => x.Id.Contains('<'));
            manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapPointExtensions").Methods.Should().BeEmpty();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_not_rebuild_anything_when_building_twice_without_changes()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            var firstBuild = GetLastWriteTimes(fixture);

            // act
            await BuildDocsAsync(fixture);

            // assert
            GetLastWriteTimes(fixture).Should().BeEquivalentTo(firstBuild);
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_update_manifest_when_referenced_public_api_changes()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            var mapModePath = Path.Combine(fixture.DocumentedProjectDirectory, "MapMode.cs");
            var mapMode = await File.ReadAllTextAsync(mapModePath);
            await File.WriteAllTextAsync(
                mapModePath,
                mapMode.Replace(
                    "Hybrid = 42,",
                    "Hybrid = 42,\n\n    /// <summary>Satellite mode.</summary>\n    Satellite = 7,",
                    StringComparison.Ordinal
                )
            );

            // act
            await BuildDocsAsync(fixture);

            // assert
            var manifest = await ReadManifestAsync(fixture);
            var satellite = manifest
                .Types.Single(x => x.Id == "DocumentedLibrary.MapMode")
                .EnumValues.Single(x => x.Name == "Satellite");
            satellite.Value.Should().Be(7);
            satellite.Summary.Should().Be("Satellite mode.");
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_update_manifest_when_only_documentation_changes()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            await ReplaceInFileAsync(
                Path.Combine(fixture.DocumentedProjectDirectory, "MapPoint.cs"),
                "A point on the map.",
                "A geographic point."
            );

            // act
            await BuildDocsAsync(fixture);

            // assert
            var manifest = await ReadManifestAsync(fixture);
            manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapPoint").Summary.Should().Be("A geographic point.");
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_not_rewrite_manifest_when_only_private_code_changes()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            var firstBuild = GetLastWriteTimes(fixture);
            await ReplaceInFileAsync(
                Path.Combine(fixture.DocumentedProjectDirectory, "TrackedEntityLayer.cs"),
                "public TItem? Find(int id) => null;",
                "public TItem? Find(int id) => id < 0 ? null : null;"
            );

            // act
            await BuildDocsAsync(fixture);

            // assert
            var secondBuild = GetLastWriteTimes(fixture);
            secondBuild[fixture.StampPath].Should().BeAfter(firstBuild[fixture.StampPath]);
            secondBuild[fixture.ManifestPath].Should().Be(firstBuild[fixture.ManifestPath]);
            secondBuild[fixture.DocsAssemblyPath].Should().Be(firstBuild[fixture.DocsAssemblyPath]);
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_regenerate_manifest_when_it_was_deleted()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            File.Delete(fixture.ManifestPath);

            // act
            await BuildDocsAsync(fixture);

            // assert
            File.Exists(fixture.ManifestPath).Should().BeTrue();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_warn_and_still_generate_manifest_when_documentation_file_is_missing()
    {
        // arrange
        var fixture = await CreateFixtureAsync(generateDocumentationFile: false);

        try
        {
            // act
            var output = await BuildDocsAsync(fixture);

            // assert
            output.Should().Contain("warning DOCSSDK001");
            var manifest = await ReadManifestAsync(fixture);
            manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapPoint").Summary.Should().BeNull();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_skip_manifest_generation_in_design_time_builds_before_the_library_is_built()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            // act
            var output = await ExecuteProcessAsync(
                "dotnet",
                $"msbuild \"{fixture.DocsProjectPath}\" -nologo -restore -t:GetCopyToOutputDirectoryItems -p:DesignTimeBuild=true -p:BuildProjectReferences=false",
                TimeSpan.FromSeconds(120)
            );

            // assert
            output.Should().NotContain("DOCSSDK");
            File.Exists(fixture.ManifestPath).Should().BeFalse();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_fail_with_clear_error_when_the_library_is_not_built()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            // act
            var output = await ExecuteProcessAsync(
                "dotnet",
                $"build \"{fixture.DocsProjectPath}\" -nologo --no-dependencies",
                TimeSpan.FromSeconds(120),
                expectSuccess: false
            );

            // assert
            output.Should().Contain("error DOCSSDK002");
            output.Should().NotContain("DOCSSDK001");
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_drop_summaries_when_the_documentation_file_is_deleted()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await BuildDocsAsync(fixture);
            foreach (var configurationDirectory in new[] { "bin", "obj" })
            {
                File.Delete(
                    Path.Combine(
                        fixture.DocumentedProjectDirectory,
                        configurationDirectory,
                        "Debug",
                        "net10.0",
                        "DocumentedLibrary.xml"
                    )
                );
            }

            // act
            var output = await ExecuteProcessAsync(
                "dotnet",
                $"build \"{fixture.DocsProjectPath}\" -nologo --no-dependencies",
                TimeSpan.FromSeconds(120)
            );

            // assert
            output.Should().Contain("warning DOCSSDK001");
            var manifest = await ReadManifestAsync(fixture);
            manifest.Types.Single(x => x.Id == "DocumentedLibrary.MapPoint").Summary.Should().BeNull();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Should_generate_and_embed_a_manifest_per_docs_api_reference()
    {
        // arrange
        var fixture = await CreateFixtureAsync();

        try
        {
            await ReplaceInFileAsync(
                fixture.DocsProjectPath,
                """<ProjectReference Include="../UnoptedLibrary/UnoptedLibrary.csproj" />""",
                """<ProjectReference Include="../UnoptedLibrary/UnoptedLibrary.csproj" DocsApi="true" />"""
            );
            await ReplaceInFileAsync(
                Path.Combine(fixture.RootDirectory, "UnoptedLibrary", "UnoptedLibrary.csproj"),
                "<Nullable>enable</Nullable>",
                "<Nullable>enable</Nullable><GenerateDocumentationFile>true</GenerateDocumentationFile>"
            );
            var secondManifestPath = Path.Combine(Path.GetDirectoryName(fixture.ManifestPath)!, "UnoptedLibrary.json");
            await BuildDocsAsync(fixture);
            var firstBuild = GetLastWriteTimes(fixture);
            var secondManifestWriteTime = File.GetLastWriteTimeUtc(secondManifestPath);

            // act
            await BuildDocsAsync(fixture);

            // assert
            GetLastWriteTimes(fixture).Should().BeEquivalentTo(firstBuild);
            File.GetLastWriteTimeUtc(secondManifestPath).Should().Be(secondManifestWriteTime);
            var docsAssembly = await File.ReadAllBytesAsync(fixture.DocsAssemblyPath);
            ContainsAscii(docsAssembly, "ApiManifest:DocumentedLibrary").Should().BeTrue();
            ContainsAscii(docsAssembly, "ApiManifest:UnoptedLibrary").Should().BeTrue();
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    [Test]
    public async Task Packed_documented_library_should_not_contain_docs_sdk_manifest_artifacts()
    {
        // arrange
        var fixture = await CreateFixtureAsync();
        var packageDirectory = Path.Combine(fixture.RootDirectory, "packages");
        Directory.CreateDirectory(packageDirectory);

        try
        {
            // act
            await ExecuteProcessAsync(
                "dotnet",
                $"pack \"{fixture.DocumentedProjectPath}\" -nologo -o \"{packageDirectory}\"",
                TimeSpan.FromSeconds(60)
            );

            // assert
            var packagePath = Directory.GetFiles(packageDirectory, "DocumentedLibrary.*.nupkg").Single();
            using var archive = ZipFile.OpenRead(packagePath);
            archive.Entries.Select(x => x.FullName).Should().NotContain(path => IsDocsSdkManifestArtifact(path));
        }
        finally
        {
            TryDeleteDirectory(fixture.RootDirectory);
        }
    }

    private static async Task<ApiManifest> ReadManifestAsync(ApiManifestFixture fixture)
    {
        await using var stream = File.OpenRead(fixture.ManifestPath);
        return (await JsonSerializer.DeserializeAsync<ApiManifest>(stream))!;
    }

    private static Task<string> BuildDocsAsync(ApiManifestFixture fixture) =>
        ExecuteProcessAsync("dotnet", $"build \"{fixture.DocsProjectPath}\" -nologo", TimeSpan.FromSeconds(120));

    private static bool ContainsAscii(byte[] bytes, string value) =>
        bytes.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(value)) >= 0;

    private static async Task ReplaceInFileAsync(string path, string oldValue, string newValue)
    {
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain(oldValue);
        await File.WriteAllTextAsync(path, content.Replace(oldValue, newValue, StringComparison.Ordinal));
    }

    private static Dictionary<string, DateTime> GetLastWriteTimes(ApiManifestFixture fixture) =>
        new[]
        {
            Path.Combine(
                fixture.DocumentedProjectDirectory,
                "obj",
                "Debug",
                "net10.0",
                "DocumentedLibrary.AssemblyInfo.cs"
            ),
            Path.Combine(fixture.DocumentedProjectDirectory, "obj", "Debug", "net10.0", "DocumentedLibrary.dll"),
            fixture.DocsAssemblyPath,
            fixture.ManifestPath,
            fixture.StampPath,
        }.ToDictionary(x => x, File.GetLastWriteTimeUtc);

    private static async Task<ApiManifestFixture> CreateFixtureAsync(bool generateDocumentationFile = true)
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "docs-sdk-api-manifest-tests",
            Guid.NewGuid().ToString("N")
        );
        var docsProjectDirectory = Path.Combine(rootDirectory, "DocsSite");
        var documentedProjectDirectory = Path.Combine(rootDirectory, "DocumentedLibrary");
        var unoptedProjectDirectory = Path.Combine(rootDirectory, "UnoptedLibrary");

        Directory.CreateDirectory(docsProjectDirectory);
        Directory.CreateDirectory(documentedProjectDirectory);
        Directory.CreateDirectory(unoptedProjectDirectory);

        var targetsPath = LocateRepositoryFile("src/Spillgebees.Blazor.Docs.Sdk/Spillgebees.Blazor.Docs.Sdk.targets");
        var normalizedTargetsPath = targetsPath.Replace("\\", "/", StringComparison.Ordinal);

        var docsProjectPath = Path.Combine(docsProjectDirectory, "DocsSite.csproj");
        await File.WriteAllTextAsync(
            docsProjectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../DocumentedLibrary/DocumentedLibrary.csproj" DocsApi="true" />
                <ProjectReference Include="../UnoptedLibrary/UnoptedLibrary.csproj" />
              </ItemGroup>
              <Import Project="{{normalizedTargetsPath}}" />
            </Project>
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "_Imports.razor"),
            """
            @using System
            """
        );

        var documentedProjectPath = Path.Combine(documentedProjectDirectory, "DocumentedLibrary.csproj");
        await File.WriteAllTextAsync(
            documentedProjectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <GenerateDocumentationFile>{{generateDocumentationFile}}</GenerateDocumentationFile>
                <PackageId>DocumentedLibrary</PackageId>
                <!-- like MinVer, the version is only set outside design-time builds -->
                <Version Condition="'$(DesignTimeBuild)' != 'true'">1.2.3</Version>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "TrackedEntityLayer.cs"),
            """
            namespace DocumentedLibrary;

            using Microsoft.AspNetCore.Components;

            /// <summary>
            /// Tracks entities on a <see href="https://example.com/map">map docs</see>.
            /// <para>See <see cref="T:DocumentedLibrary.MapMode" /> for display modes.</para>
            /// <list type="bullet">
            /// <item><description>Supports <c>tracked items</c>.</description></item>
            /// </list>
            /// </summary>
            /// <remarks>
            /// <code>
            /// var layer = new TrackedEntityLayer&lt;string&gt;();
            /// </code>
            /// </remarks>
            /// <typeparam name="TItem">Entity type.</typeparam>
            public class TrackedEntityLayer<TItem> : ComponentBase
                where TItem : class
            {
                /// <summary>
                /// Gets or sets tracked items.
                /// </summary>
                [Parameter]
                public IReadOnlyList<TItem>? Items { get; set; }

                /// <summary>
                /// Gets or sets the inherited theme.
                /// </summary>
                [CascadingParameter]
                public string? Theme { get; set; }

                /// <summary>
                /// Finds an entity by <paramref name="id" />.
                /// </summary>
                /// <param name="id">Entity id.</param>
                /// <param name="includeInactive">Whether inactive entities are searched.</param>
                public TItem? Find(string id, bool includeInactive = false) => null;

                /// <summary>
                /// Finds an entity by numeric id.
                /// </summary>
                public TItem? Find(int id) => null;
            }
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "MapMode.cs"),
            """
            namespace DocumentedLibrary;

            /// <summary>
            /// Map display modes.
            /// </summary>
            public enum MapMode
            {
                /// <summary>Road mode.</summary>
                Road = 1,

                /// <summary>Hybrid mode.</summary>
                Hybrid = 42,
            }
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "MapLegend.razor"),
            """
            @namespace DocumentedLibrary
            @inherits Microsoft.AspNetCore.Components.ComponentBase

            <p>@Title</p>

            @code {
                /// <summary>Legend title.</summary>
                [Microsoft.AspNetCore.Components.Parameter]
                public string? Title { get; set; }
            }
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "MapPoint.cs"),
            """
            namespace DocumentedLibrary;

            /// <summary>A point on the map.</summary>
            public record MapPoint(double Latitude, double Longitude);
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "MapPointExtensions.cs"),
            """
            namespace DocumentedLibrary;

            /// <summary>Map point helpers.</summary>
            public static class MapPointExtensions
            {
                extension(MapPoint point)
                {
                    /// <summary>Whether the point is on the equator.</summary>
                    public bool IsOnEquator => point.Latitude == 0;
                }
            }
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(documentedProjectDirectory, "InternalHelper.cs"),
            """
            namespace DocumentedLibrary;

            /// <summary>
            /// This documented helper must not appear in the manifest.
            /// </summary>
            internal class InternalHelper;
            """
        );

        await File.WriteAllTextAsync(
            Path.Combine(unoptedProjectDirectory, "UnoptedLibrary.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """
        );
        await File.WriteAllTextAsync(
            Path.Combine(unoptedProjectDirectory, "UnoptedType.cs"),
            """
            namespace UnoptedLibrary;

            public class UnoptedType;
            """
        );

        return new ApiManifestFixture(
            rootDirectory,
            docsProjectDirectory,
            docsProjectPath,
            documentedProjectDirectory,
            documentedProjectPath
        );
    }

    private static async Task<string> ExecuteProcessAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        bool expectSuccess = true
    )
    {
        using var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        );
        process.Should().NotBeNull();

        var outputTask = process!.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        var output = await outputTask;
        var error = await errorTask;

        if (timedOut)
        {
            throw new InvalidOperationException(
                $"Process timed out after {timeout}. Output:\n{output}\nErrors:\n{error}"
            );
        }

        (process.ExitCode == 0)
            .Should()
            .Be(expectSuccess, $"process exited with code {process.ExitCode}. Output:\n{output}\nErrors:\n{error}");

        return output + error;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool IsDocsSdkManifestArtifact(string path)
    {
        var fileName = Path.GetFileName(path);
        var isJson = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        return isJson
            && (
                path.Contains("DocsSdk", StringComparison.OrdinalIgnoreCase)
                || path.Contains("ApiManifest", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/manifests/", StringComparison.OrdinalIgnoreCase)
            );
    }

    private static string LocateRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "Blazor.Docs.Sdk.slnx");
            if (File.Exists(solutionPath))
            {
                return Path.Combine(current.FullName, relativePath);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test execution directory.");
    }

    private sealed record ApiManifestFixture(
        string RootDirectory,
        string DocsProjectDirectory,
        string DocsProjectPath,
        string DocumentedProjectDirectory,
        string DocumentedProjectPath
    )
    {
        public string DocsAssemblyPath => Path.Combine(DocsProjectDirectory, "obj", "Debug", "net10.0", "DocsSite.dll");

        public string StampPath =>
            Path.Combine(DocsProjectDirectory, "obj", "Debug", "net10.0", "DocsSdk", "api-manifest.stamp");

        public string ManifestPath =>
            Path.Combine(
                DocsProjectDirectory,
                "obj",
                "Debug",
                "net10.0",
                "DocsSdk",
                "manifests",
                "DocumentedLibrary.json"
            );
    }
}
