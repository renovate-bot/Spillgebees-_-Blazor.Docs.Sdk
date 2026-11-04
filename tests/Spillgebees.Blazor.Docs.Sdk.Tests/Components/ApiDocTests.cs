using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class ApiDocTests
{
    private static ApiTypeInfo CreateTestType() =>
        new()
        {
            Name = "SgbMap",
            FullName = "Spillgebees.Blazor.Map.SgbMap",
            Namespace = "Spillgebees.Blazor.Map",
            Kind = "class",
            Summary = "A map component",
            Properties =
            [
                new ApiPropertyInfo
                {
                    Name = "Center",
                    Type = "LatLng",
                    Summary = "Map center position",
                    IsParameter = true,
                },
            ],
            Methods =
            [
                new ApiMethodInfo
                {
                    Name = "FlyTo",
                    ReturnType = "Task",
                    Summary = "Animate to position",
                    Parameters = [new ApiParameterInfo { Name = "position", Type = "LatLng" }],
                },
            ],
        };

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightAll", _ => true);
        return ctx;
    }

    [Test]
    public void Should_render_type_name()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Find("h1").TextContent.Should().Be("SgbMap");
    }

    [Test]
    public void Should_let_a_long_type_name_wrap_between_words()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = new ApiTypeInfo
        {
            Name = "ApiManifestGenerator",
            FullName = "Spillgebees.Blazor.Map.Example",
            Namespace = "Spillgebees.Blazor.Map",
            Kind = "class",
        };

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        var title = cut.Find("h1");
        title.InnerHtml.Should().Be("Api<wbr>Manifest<wbr>Generator");
        title.TextContent.Should().Be("ApiManifestGenerator");
    }

    [Test]
    public void Should_encode_a_generic_type_name_in_the_title()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = new ApiTypeInfo
        {
            Name = "ExampleView<TComponent>",
            FullName = "Spillgebees.Blazor.Map.Example",
            Namespace = "Spillgebees.Blazor.Map",
            Kind = "class",
        };

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Find("h1").TextContent.Should().Be("ExampleView<TComponent>");
    }

    [Test]
    public void Should_render_namespace()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Markup.Should().Contain("Spillgebees.Blazor.Map");
    }

    [Test]
    public void Should_link_a_property_to_its_anchor_on_the_current_page()
    {
        // arrange
        using var ctx = CreateContext();
        ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("api/SgbMap?theme=refined#member-Other");

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, CreateTestType()));

        // assert
        cut.Find("a.docs-api-name")
            .GetAttribute("href")
            .Should()
            .Be("http://localhost/api/SgbMap?theme=refined#member-Center");
    }

    [Test]
    public void Should_render_properties()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        var rows = cut.FindAll(".docs-api-table tbody tr");
        rows.Should().NotBeEmpty();
        cut.Markup.Should().Contain("Center");
        cut.Markup.Should().Contain("LatLng");
    }

    [Test]
    public void Should_render_short_type_names_with_full_type_tooltips()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();
        typeInfo.Properties[0].Type =
            "System.Collections.Generic.IReadOnlyDictionary<string, Spillgebees.Blazor.Docs.Sdk.NavSection>";

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        var typeBadge = cut.Find(".docs-api-type");
        typeBadge.TextContent.Should().Be("IReadOnlyDictionary<string, NavSection>");
        typeBadge
            .GetAttribute("title")
            .Should()
            .Be("System.Collections.Generic.IReadOnlyDictionary<string, Spillgebees.Blazor.Docs.Sdk.NavSection>");
    }

    [Test]
    public void Should_render_methods()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Markup.Should().Contain("FlyTo");
        cut.Markup.Should().Contain("Task");
    }

    [Test]
    public void Should_render_rich_xml_documentation_html()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();
        typeInfo.Summary = "Plain fallback";
        typeInfo.SummaryHtml =
            """Use <a href="https://example.com" target="_blank" rel="noopener noreferrer">docs</a> and <code>code</code>.""";
        typeInfo.Methods[0].Parameters[0].Summary = "Position fallback";
        typeInfo.Methods[0].Parameters[0].SummaryHtml = "The <code>position</code> value.";

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Markup.Should()
            .Contain("""<a href="https://example.com" target="_blank" rel="noopener noreferrer">docs</a>""");
        cut.Markup.Should().Contain("<code>code</code>");
        cut.Markup.Should().Contain("The <code>position</code> value.");
    }

    [Test]
    public void Should_render_parameter_badge_for_blazor_parameters()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        var badge = cut.Find(".docs-badge-required");
        badge.Should().NotBeNull();
        badge.TextContent.Should().Contain("parameter");
    }

    [Test]
    public void Should_label_public_properties_section_without_confusing_it_with_blazor_parameters()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        cut.Markup.Should().Contain("Properties");
        cut.Markup.Should().NotContain("// parameters");
    }

    [Test]
    public void Should_render_collapsible_section_toggles()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));

        // assert
        var toggles = cut.FindAll(".docs-api-group-head");
        toggles.Should().NotBeEmpty();
    }

    [Test]
    public void Should_collapse_section_when_toggle_is_clicked()
    {
        // arrange
        using var ctx = CreateContext();
        var typeInfo = CreateTestType();
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, typeInfo));
        var group = cut.Find(".docs-api-group");
        group.GetAttribute("data-docs-open").Should().Be("true");

        // act
        cut.FindAll(".docs-api-group-head")[0].Click();

        // assert
        cut.Find(".docs-api-group").GetAttribute("data-docs-open").Should().Be("false");
    }

    [Test]
    public void Should_reset_the_member_filter_when_the_documented_type_changes()
    {
        // arrange
        using var ctx = CreateContext();
        var first = CreateTestType();
        first.Id = "T:First";
        var second = CreateTestType();
        second.Id = "T:Second";
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, first));
        cut.Find(".docs-api-filter input").Input("no such member");
        cut.FindAll(".docs-api-empty").Should().HaveCount(1);

        // act
        cut.Render(parameters => parameters.Add(p => p.TypeInfo, second));

        // assert
        cut.FindAll(".docs-api-empty").Should().BeEmpty();
        cut.Find(".docs-api-filter input").GetAttribute("value").Should().BeEmpty();
    }

    [Test]
    public void Should_keep_the_member_filter_when_the_same_type_is_passed_again()
    {
        // arrange
        using var ctx = CreateContext();
        var type = CreateTestType();
        type.Id = "T:Same";
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, type));
        cut.Find(".docs-api-filter input").Input("no such member");

        // act: a parent re-render hands over a fresh object for the same type
        var copy = CreateTestType();
        copy.Id = "T:Same";
        cut.Render(parameters => parameters.Add(p => p.TypeInfo, copy));

        // assert
        cut.FindAll(".docs-api-empty").Should().HaveCount(1);
    }

    [Test]
    public void Should_reset_parameters_only_and_collapsed_groups_when_the_type_changes()
    {
        // arrange
        using var ctx = CreateContext();
        var first = CreateTestType();
        first.Id = "T:First";
        var second = CreateTestType();
        second.Id = "T:Second";
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, first));
        cut.Find(".docs-api-switch input").Change(true);
        cut.FindAll(".docs-api-group-head")[0].Click();

        // act
        cut.Render(parameters => parameters.Add(p => p.TypeInfo, second));

        // assert
        cut.Find(".docs-api-switch input").HasAttribute("checked").Should().BeFalse();
        cut.FindAll(".docs-api-group")
            .Should()
            .AllSatisfy(group => group.GetAttribute("data-docs-open").Should().Be("true"));
    }

    [Test]
    public void Should_put_member_badges_on_their_own_row_under_the_name()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ApiDoc>(parameters => parameters.Add(p => p.TypeInfo, CreateTestType()));

        // assert: a separate row keeps every parameter row the same shape,
        // however long the member name is
        var tags = cut.Find(".docs-api-name + .docs-api-tags");
        tags.QuerySelectorAll(".docs-badge").Select(badge => badge.TextContent).Should().Equal("parameter");
    }
}
