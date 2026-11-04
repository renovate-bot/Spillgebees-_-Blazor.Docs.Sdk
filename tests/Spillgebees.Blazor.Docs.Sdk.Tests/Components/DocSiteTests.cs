using AwesomeAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class DocSiteTests
{
    private static NavSection[] CreateTestNav() =>
        [new("Getting Started", [new NavPage("Install", "install"), new NavPage("Configure", "configure")])];

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getSchemePreference").SetResult("system");
        ctx.JSInterop.Setup<string>("Spillgebees.DocsSdk.getTheme").SetResult("refined");
        ctx.JSInterop.Setup<OnThisPageSection[]>("Spillgebees.DocsSdk.getSections").SetResult([]);
        return ctx;
    }

    private static IRenderedComponent<DocSite> RenderSite(
        BunitContext ctx,
        Action<ComponentParameterCollectionBuilder<DocSite>>? extra = null
    ) =>
        ctx.Render<DocSite>(parameters =>
        {
            parameters
                .Add(p => p.ProjectName, "MyProject")
                .Add(p => p.LogoEmoji, "🚀")
                .Add(p => p.GitHubUrl, "https://github.com/test/repo")
                .Add(p => p.Navigation, CreateTestNav());
            extra?.Invoke(parameters);
        });

    [Test]
    public void Should_render_project_name_and_logo()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.Markup.Should().Contain("MyProject").And.Contain("🚀");
    }

    [Test]
    public void Should_render_child_content()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(
            ctx,
            parameters =>
                parameters.Add(
                    p => p.ChildContent,
                    (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Hello from child</p>"))
                )
        );

        // assert
        cut.Find(".docs-content-inner").TextContent.Should().Contain("Hello from child");
    }

    [Test]
    public void Should_render_a_navigation_link_per_page()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.FindAll(".docs-nav-group a").Should().HaveCount(2);
    }

    [Test]
    public void Should_render_a_skip_link_targeting_the_main_region()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.Find("a.docs-skip").GetAttribute("href").Should().Be("#docs-main");
        cut.Find("#docs-main").Should().NotBeNull();
    }

    [Test]
    public void Should_render_a_search_trigger()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.Find(".docs-search-trigger").Should().NotBeNull();
    }

    [Test]
    public void Should_apply_the_configured_theme_on_first_render()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        RenderSite(ctx, parameters => parameters.Add(p => p.Theme, DocsTheme.Terminal));

        // assert
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation =>
                invocation.Identifier == "Spillgebees.DocsSdk.initializeTheming"
                && invocation.Arguments.Contains("terminal")
            );
    }

    [Test]
    public void Should_hide_the_theme_picker_by_default()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.FindAll(".docs-theme-picker").Should().BeEmpty();
    }

    [Test]
    public void Should_show_the_theme_picker_when_enabled()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx, parameters => parameters.Add(p => p.ShowThemePicker, true));

        // assert: one copy in the top bar and one in the mobile drawer, so the
        // control stays reachable when the top bar sheds it on narrow viewports
        cut.FindAll(".docs-topbar .docs-theme-picker").Should().ContainSingle();
        cut.FindAll(".docs-drawer-actions .docs-theme-picker").Should().ContainSingle();
        // the picker is a themed listbox rather than a native select, so the
        // options only exist once the menu is open
        cut.Find(".docs-topbar .docs-theme-picker .docs-select-trigger").Click();
        cut.FindAll(".docs-topbar .docs-theme-picker .docs-select-menu [role='option']")
            .Should()
            .HaveCount(DocsTheme.BuiltIn.Count);
    }

    [Test]
    public void Should_keep_the_colour_scheme_toggle_reachable_in_the_drawer()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.FindAll(".docs-topbar .docs-theme-toggle").Should().ContainSingle();
        cut.FindAll(".docs-drawer-actions .docs-theme-toggle").Should().ContainSingle();
    }

    [Test]
    public void Should_render_the_on_this_page_list_both_beside_and_above_the_content()
    {
        // arrange
        using var ctx = CreateContext();
        ctx.JSInterop.Setup<OnThisPageSection[]>("Spillgebees.DocsSdk.getSections")
            .SetResult([new OnThisPageSection("Usage", "usage")]);

        // act
        var cut = RenderSite(ctx);

        // assert: the inline copy must sit inside the content, above it, rather
        // than being stranded after it in source order
        cut.WaitForAssertion(() =>
        {
            cut.FindAll(".docs-toc").Should().ContainSingle();
            cut.FindAll(".docs-content-inner .docs-toc-mobile").Should().ContainSingle();
        });
    }

    [Test]
    public void Should_omit_the_version_selector_when_no_versions_are_supplied()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(ctx);

        // assert
        cut.FindAll(".docs-version-select").Should().BeEmpty();
    }

    [Test]
    public void Should_render_versions_when_supplied()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = RenderSite(
            ctx,
            parameters => parameters.Add(p => p.Versions, ["1.0.0", "2.0.0"]).Add(p => p.CurrentVersion, "2.0.0")
        );

        // assert: again duplicated into the drawer
        cut.FindAll(".docs-topbar .docs-version-select").Should().ContainSingle();
        cut.FindAll(".docs-drawer-actions .docs-version-select").Should().ContainSingle();

        cut.Find(".docs-topbar .docs-version-select .docs-select-trigger").Click();
        cut.FindAll(".docs-topbar .docs-version-select .docs-select-menu [role='option']").Should().HaveCount(2);
    }

    [Test]
    public void Should_not_render_a_github_link_for_a_non_http_url()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        var cut = ctx.Render<DocSite>(parameters =>
            parameters
                .Add(p => p.ProjectName, "MyProject")
                .Add(p => p.LogoEmoji, "🚀")
                .Add(p => p.GitHubUrl, "javascript:alert(1)")
                .Add(p => p.Navigation, CreateTestNav())
        );

        // assert
        cut.FindAll(".docs-footer-mark").Should().BeEmpty();
        cut.FindAll("a[href^='javascript:']").Should().BeEmpty();
    }

    [Test]
    public void Should_pass_the_offered_themes_so_a_stored_theme_is_validated()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        RenderSite(
            ctx,
            parameters =>
                parameters
                    .Add(p => p.ShowThemePicker, true)
                    .Add(p => p.AvailableThemes, [DocsTheme.Refined, DocsTheme.GloryToMankind])
        );

        // assert
        var invocation = ctx.JSInterop.Invocations["Spillgebees.DocsSdk.initializeTheming"].Single();
        invocation.Arguments[1].Should().BeEquivalentTo(new[] { "refined", "glory-to-mankind" });
    }

    [Test]
    public void Should_ignore_stored_themes_when_visitors_cannot_pick_one()
    {
        // arrange
        using var ctx = CreateContext();

        // act
        RenderSite(ctx);

        // assert
        var invocation = ctx.JSInterop.Invocations["Spillgebees.DocsSdk.initializeTheming"].Single();
        invocation.Arguments[1].Should().BeNull();
    }

    [Test]
    public void Should_close_the_drawer_when_the_page_changes_from_outside_it()
    {
        // arrange
        using var ctx = CreateContext();
        var cut = RenderSite(ctx);
        cut.Find(".docs-hamburger").Click();
        cut.Find(".docs-hamburger").GetAttribute("aria-expanded").Should().Be("true");

        // act: e.g. following a search result
        cut.InvokeAsync(() => ctx.Services.GetRequiredService<NavigationManager>().NavigateTo("installation"));

        // assert
        cut.WaitForAssertion(() => cut.Find(".docs-hamburger").GetAttribute("aria-expanded").Should().Be("false"));
        ctx.JSInterop.Invocations["Spillgebees.DocsSdk.setNavOpen"].Last().Arguments[0].Should().Be(false);
    }

    [Test]
    public void Should_turn_the_menu_button_into_the_drawer_close_button()
    {
        // arrange
        using var ctx = CreateContext();
        var cut = RenderSite(ctx);
        cut.Find(".docs-hamburger").GetAttribute("aria-label").Should().Be("Open navigation");

        // act
        cut.Find(".docs-hamburger").Click();

        // assert: with no header in the drawer, this is its visible close control
        cut.Find(".docs-hamburger").GetAttribute("aria-label").Should().Be("Close navigation");
        cut.FindAll(".docs-sidebar button[aria-label='Close navigation']").Should().BeEmpty();

        cut.Find(".docs-hamburger").Click();
        cut.Find(".docs-hamburger").GetAttribute("aria-label").Should().Be("Open navigation");
    }
}
