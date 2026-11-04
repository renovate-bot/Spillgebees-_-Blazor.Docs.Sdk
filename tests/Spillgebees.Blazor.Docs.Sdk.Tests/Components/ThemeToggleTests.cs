using AwesomeAssertions;
using Microsoft.JSInterop;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class ThemeToggleTests
{
    private static BunitContext CreateContext(string storedPreference)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getSchemePreference").SetResult(storedPreference);
        return ctx;
    }

    [Test]
    public void Should_render_a_labelled_button()
    {
        // arrange
        using var ctx = CreateContext("system");

        // act
        var cut = ctx.Render<ThemeToggle>();

        // assert
        cut.Find("button").GetAttribute("aria-label").Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Should_reflect_the_stored_preference()
    {
        // arrange
        using var ctx = CreateContext("light");

        // act
        var cut = ctx.Render<ThemeToggle>();

        // assert
        cut.WaitForAssertion(() => cut.Find("button").GetAttribute("aria-label").Should().Contain("light"));
    }

    [Test]
    public void Should_default_to_following_the_system()
    {
        // arrange
        using var ctx = CreateContext("system");

        // act
        var cut = ctx.Render<ThemeToggle>();

        // assert
        cut.WaitForAssertion(() => cut.Find("button").GetAttribute("aria-label").Should().Contain("system"));
    }

    [Test]
    public void Should_cycle_the_preference_on_click()
    {
        // arrange
        using var ctx = CreateContext("system");
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.cycleSchemePreference").SetResult("dark");
        var cut = ctx.Render<ThemeToggle>();

        // act
        cut.Find("button").Click();

        // assert
        cut.WaitForAssertion(() => cut.Find("button").GetAttribute("aria-label").Should().Contain("dark"));
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation => invocation.Identifier == "Spillgebees.DocsSdk.cycleSchemePreference");
    }

    [Test]
    public void Should_raise_on_scheme_changed()
    {
        // arrange
        using var ctx = CreateContext("system");
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.cycleSchemePreference").SetResult("dark");
        DocsColorScheme? observed = null;
        var cut = ctx.Render<ThemeToggle>(parameters =>
            parameters.Add(p => p.OnSchemeChanged, scheme => observed = scheme)
        );

        // act
        cut.Find("button").Click();

        // assert
        cut.WaitForAssertion(() => observed.Should().Be(DocsColorScheme.Dark));
    }

    [Test]
    public void Should_still_advance_when_the_module_is_unavailable()
    {
        // arrange: the module failed to load, so every call throws
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getSchemePreference")
            .SetException(new JSException("module not loaded"));
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.cycleSchemePreference")
            .SetException(new JSException("module not loaded"));
        var cut = ctx.Render<ThemeToggle>();

        // act
        cut.Find("button").Click();

        // assert
        cut.WaitForAssertion(() => cut.Find("button").GetAttribute("aria-label").Should().Contain("light"));
    }
}
