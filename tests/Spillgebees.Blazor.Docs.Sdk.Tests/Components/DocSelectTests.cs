using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class DocSelectTests
{
    private static IRenderedComponent<DocSelect<string>> RenderSelect(
        BunitContext ctx,
        Action<string>? onChanged = null
    )
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        return ctx.Render<DocSelect<string>>(parameters =>
            parameters
                .Add(p => p.Items, ["one", "two"])
                .Add(p => p.Value, "one")
                .Add(p => p.AriaLabel, "Pick")
                .Add(p => p.ValueChanged, onChanged ?? (_ => { }))
        );
    }

    [Test]
    public void Should_expose_options_without_nesting_them_in_another_option()
    {
        // arrange
        using var ctx = new BunitContext();
        var cut = RenderSelect(ctx);

        // act
        cut.Find(".docs-select-trigger").Click();

        // assert
        var options = cut.FindAll("[role='option']");
        options.Should().HaveCount(2);
        options.Should().AllSatisfy(option => option.TagName.Should().Be("BUTTON"));
        options[0].GetAttribute("aria-selected").Should().Be("true");
        cut.FindAll("li[role='option']").Should().BeEmpty();
    }

    [Test]
    public void Should_close_and_report_the_value_when_an_option_is_picked()
    {
        // arrange
        using var ctx = new BunitContext();
        string? picked = null;
        var cut = RenderSelect(ctx, value => picked = value);
        cut.Find(".docs-select-trigger").Click();

        // act
        cut.FindAll("[role='option']")[1].Click();

        // assert
        picked.Should().Be("two");
        cut.FindAll(".docs-select-menu").Should().BeEmpty();
        cut.Find(".docs-select-trigger").GetAttribute("aria-expanded").Should().Be("false");
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation => invocation.Identifier == "Spillgebees.DocsSdk.focusElement");
    }

    [Test]
    public void Should_close_when_clicking_away()
    {
        // arrange
        using var ctx = new BunitContext();
        var cut = RenderSelect(ctx);
        cut.Find(".docs-select-trigger").Click();

        // act
        cut.Find(".docs-select-scrim").Click();

        // assert
        cut.FindAll(".docs-select-menu").Should().BeEmpty();
    }
}
