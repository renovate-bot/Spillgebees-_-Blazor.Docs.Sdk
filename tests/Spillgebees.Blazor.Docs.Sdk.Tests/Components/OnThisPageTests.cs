using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class OnThisPageTests
{
    private static BunitContext CreateContext(params OnThisPageSection[] sections)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Setup<OnThisPageSection[]>("Spillgebees.DocsSdk.getSections").SetResult(sections);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.observeSections", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.scrollToElement", _ => true);
        return ctx;
    }

    [Test]
    public void Should_render_a_link_per_section()
    {
        // arrange
        using var ctx = CreateContext(
            new OnThisPageSection("Introduction", "introduction"),
            new OnThisPageSection("Usage", "usage")
        );

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.WaitForAssertion(() =>
        {
            var links = cut.FindAll(".docs-toc a");
            links.Should().HaveCount(2);
            links[0].TextContent.Trim().Should().Be("Introduction");
            links[1].TextContent.Trim().Should().Be("Usage");
        });
    }

    [Test]
    public void Should_tag_links_for_the_scroll_spy()
    {
        // arrange
        using var ctx = CreateContext(new OnThisPageSection("Usage", "usage"));

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.WaitForAssertion(() => cut.Find(".docs-toc a").GetAttribute("data-docs-toc-link").Should().Be("usage"));
    }

    [Test]
    public void Should_indent_nested_headings()
    {
        // arrange
        using var ctx = CreateContext(
            new OnThisPageSection("Usage", "usage"),
            new OnThisPageSection("Details", "details", 3)
        );

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll(".docs-toc li");
            items[0].ClassList.Should().NotContain("docs-toc-h3");
            items[1].ClassList.Should().Contain("docs-toc-h3");
        });
    }

    [Test]
    public void Should_also_render_a_mobile_disclosure()
    {
        // arrange
        using var ctx = CreateContext(new OnThisPageSection("Usage", "usage"));

        // act
        var cut = ctx.Render<OnThisPage>(p => p.Add(x => x.Placement, OnThisPagePlacement.Inline));

        // assert: the inline placement is a disclosure, not the rail
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".docs-toc-mobile a").Should().HaveCount(1);
            cut.FindAll(".docs-toc").Should().BeEmpty();
        });
    }

    [Test]
    public void Should_start_the_scroll_spy()
    {
        // arrange
        using var ctx = CreateContext(new OnThisPageSection("Usage", "usage"));

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.WaitForAssertion(() =>
            ctx
                .JSInterop.Invocations.Should()
                .Contain(invocation => invocation.Identifier == "Spillgebees.DocsSdk.observeSections")
        );
    }

    [Test]
    public void Should_render_nothing_when_the_page_has_no_headings()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.FindAll(".docs-toc a").Should().BeEmpty();
        cut.FindAll(".docs-toc-mobile").Should().BeEmpty();
    }

    [Test]
    public void Should_treat_null_sections_as_empty()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<OnThisPageSection[]>("Spillgebees.DocsSdk.getSections").SetResult(null!);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.observeSections", _ => true);

        // act
        var cut = ctx.Render<OnThisPage>();

        // assert
        cut.FindAll(".docs-toc a").Should().BeEmpty();
    }
}
