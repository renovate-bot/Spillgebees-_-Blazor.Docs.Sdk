using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class BreadcrumbTests
{
    [Test]
    public void Should_render_section_and_page_name()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = ctx.Render<Breadcrumb>(p => p.Add(x => x.Section, "Components").Add(x => x.PageName, "ExampleView"));

        // assert
        cut.Find(".docs-breadcrumb").TextContent.Should().Contain("Components").And.Contain("ExampleView");
    }

    [Test]
    public void Should_mark_the_page_name_as_current()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = ctx.Render<Breadcrumb>(p => p.Add(x => x.Section, "Components").Add(x => x.PageName, "ExampleView"));

        // assert
        cut.Find("[aria-current='page']").TextContent.Should().Be("ExampleView");
    }

    [Test]
    public void Should_render_the_section_as_plain_text_without_a_href()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = ctx.Render<Breadcrumb>(p => p.Add(x => x.Section, "Components").Add(x => x.PageName, "ExampleView"));

        // assert
        cut.FindAll(".docs-breadcrumb a").Should().BeEmpty();
    }

    [Test]
    public void Should_link_the_section_when_a_href_is_supplied()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = ctx.Render<Breadcrumb>(p =>
            p.Add(x => x.Section, "Components")
                .Add(x => x.PageName, "ExampleView")
                .Add(x => x.SectionHref, "components")
        );

        // assert
        var link = cut.Find(".docs-breadcrumb a");
        link.GetAttribute("href").Should().Be("components");
        link.TextContent.Should().Be("Components");
    }
}
