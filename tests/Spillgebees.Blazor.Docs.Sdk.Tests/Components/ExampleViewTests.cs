using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class StubComponent : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, "stub");
    }
}

public class ExampleViewTests
{
    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        return ctx;
    }

    [Test]
    public void Should_render_child_content_in_preview_mode()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>(parameters =>
            parameters.AddChildContent("<span id=\"child\">child content</span>")
        );

        // assert
        cut.Find("#child").TextContent.Should().Be("child content");
    }

    [Test]
    public void Should_render_preview_source_and_split_modes()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>();

        // assert
        var modes = cut.FindAll(".docs-example-seg button");
        modes.Should().HaveCount(3);
        modes[0].TextContent.Should().Contain("Preview");
        modes[1].TextContent.Should().Contain("Source");
        modes[2].TextContent.Should().Contain("Split");
    }

    [Test]
    public void Should_start_in_preview_mode_by_default()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>();

        // assert
        cut.Find(".docs-example").GetAttribute("data-docs-mode").Should().Be("preview");
    }

    [Test]
    public void Should_switch_mode_on_click()
    {
        // arrange
        using var ctx = CreateContext();
        var cut = ctx.Render<ExampleView<StubComponent>>();

        // act
        cut.FindAll(".docs-example-seg button")[2].Click();

        // assert
        cut.Find(".docs-example").GetAttribute("data-docs-mode").Should().Be("split");
    }

    [Test]
    public void Should_fall_back_to_preview_when_default_is_source_but_none_was_extracted()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>(parameters =>
            parameters.Add(p => p.DefaultMode, ExampleMode.Source)
        );

        // assert
        cut.Find(".docs-example").GetAttribute("data-docs-mode").Should().Be("preview");
    }

    [Test]
    public void Should_explain_when_no_source_was_extracted()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>();

        // assert
        cut.Find(".docs-example-empty").TextContent.Should().Contain("StubComponent");
    }

    [Test]
    public void Should_clamp_the_preview_when_a_viewport_is_selected()
    {
        // arrange
        using var ctx = CreateContext();
        var cut = ctx.Render<ExampleView<StubComponent>>();

        // act
        cut.FindAll(".docs-example-viewports button")[2].Click();

        // assert
        cut.Find(".docs-example-preview").GetAttribute("data-docs-viewport").Should().Be("sm");
    }

    [Test]
    public void Should_hide_viewport_controls_when_disabled()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<ExampleView<StubComponent>>(parameters =>
            parameters.Add(p => p.ShowViewportControls, false)
        );

        // assert
        cut.FindAll(".docs-example-viewports").Should().BeEmpty();
    }

    [Test]
    public void Should_raise_on_mode_changed()
    {
        // arrange
        using var ctx = CreateContext();
        ExampleMode? observed = null;
        var cut = ctx.Render<ExampleView<StubComponent>>(parameters =>
            parameters.Add(p => p.OnModeChanged, mode => observed = mode)
        );

        // act
        cut.FindAll(".docs-example-seg button")[1].Click();

        // assert
        observed.Should().Be(ExampleMode.Source);
    }

    [Test]
    public async Task Should_await_an_asynchronous_mode_callback()
    {
        // arrange
        using var ctx = CreateContext();
        var cut = ctx.Render<ExampleView<StubComponent>>(parameters =>
            parameters.Add(
                p => p.OnModeChanged,
                async (ExampleMode _) =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("callback failed");
                }
            )
        );

        // act
        await cut.FindAll(".docs-example-seg button")[1].ClickAsync(new MouseEventArgs());

        // assert: a fire-and-forget call would lose this exception instead of
        // handing it to the renderer
        var exception = await ctx.Renderer.UnhandledException.WaitAsync(TimeSpan.FromSeconds(5));
        exception.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("callback failed");
    }
}
