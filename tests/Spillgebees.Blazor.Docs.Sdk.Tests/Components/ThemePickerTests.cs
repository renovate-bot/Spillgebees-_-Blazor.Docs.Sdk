using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class ThemePickerTests
{
    [Test]
    public void Should_label_themes_with_their_display_name()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getTheme").SetResult("glory-to-mankind");

        // act
        var cut = ctx.Render<ThemePicker>(parameters => parameters.Add(p => p.DefaultTheme, DocsTheme.GloryToMankind));

        // assert
        cut.WaitForAssertion(() => cut.Find(".docs-select-value").TextContent.Should().Be("Glory to Mankind"));
    }

    [Test]
    public void Should_offer_the_default_theme_even_when_it_is_not_listed()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getTheme").SetResult("terminal");
        var cut = ctx.Render<ThemePicker>(parameters =>
            parameters
                .Add(p => p.DefaultTheme, DocsTheme.Terminal)
                .Add(p => p.Themes, [DocsTheme.Refined, DocsTheme.GloryToMankind])
        );

        // act
        cut.Find(".docs-select-trigger").Click();

        // assert
        cut.FindAll("[role='option']")
            .Select(option => option.TextContent.Trim())
            .Should()
            .Equal("Terminal", "Refined", "Glory to Mankind");
    }
}
