using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class DocSearchTests
{
    // interleaved groups: a naive render would print "Guides" twice
    private static readonly IReadOnlyList<DocSearchEntry> Entries =
    [
        new("Install", "install", "Guides"),
        new("DocSite", "components/doc-site", "Components"),
        new("Theming", "theming", "Guides"),
    ];

    private static async Task<IRenderedComponent<DocSearch>> RenderOpenAsync(BunitContext ctx)
    {
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = ctx.Render<DocSearch>(parameters => parameters.Add(p => p.Entries, Entries));
        await cut.InvokeAsync(() => cut.Instance.OpenAsync());
        return cut;
    }

    [Test]
    public async Task Should_render_each_group_header_once()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = await RenderOpenAsync(ctx);

        // assert
        cut.FindAll(".docs-modal-group").Select(header => header.TextContent).Should().Equal("Guides", "Components");
        cut.FindAll("[role='group']")[0]
            .QuerySelectorAll("[role='option']")
            .Select(row => row.TextContent.Trim())
            .Should()
            .HaveCount(2);
    }

    [Test]
    public async Task Should_point_the_query_field_at_the_active_result()
    {
        // arrange
        using var ctx = new BunitContext();
        var cut = await RenderOpenAsync(ctx);
        var input = cut.Find("input[role='combobox']");
        var first = input.GetAttribute("aria-activedescendant");

        // act
        cut.Find(".docs-modal").KeyDown("ArrowDown");

        // assert
        var next = cut.Find("input[role='combobox']").GetAttribute("aria-activedescendant");
        next.Should().NotBe(first);
        cut.Find($"#{next}").GetAttribute("aria-selected").Should().Be("true");
    }

    [Test]
    public async Task Should_keep_result_rows_out_of_the_tab_order()
    {
        // arrange
        using var ctx = new BunitContext();

        // act
        var cut = await RenderOpenAsync(ctx);

        // assert: focus stays in the query field, so Enter always opens the
        // highlighted result
        cut.FindAll(".docs-modal-row")
            .Should()
            .AllSatisfy(row =>
            {
                new[] { "A", "BUTTON", "INPUT" }.Should().NotContain(row.TagName);
                row.HasAttribute("tabindex").Should().BeFalse();
                row.QuerySelectorAll("a, button, input, [tabindex]").Should().BeEmpty();
            });
    }

    [Test]
    public async Task Should_release_its_own_scroll_lock_on_close()
    {
        // arrange
        using var ctx = new BunitContext();
        var cut = await RenderOpenAsync(ctx);

        // act
        await cut.InvokeAsync(() => cut.Instance.CloseAsync());

        // assert: the lock is per owner, so closing search cannot unlock an open drawer
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation =>
                invocation.Identifier == "Spillgebees.DocsSdk.setScrollLock"
                && Equals(invocation.Arguments[0], "search")
                && Equals(invocation.Arguments[1], false)
            );
    }

    [Test]
    public async Task Should_release_its_scroll_lock_and_focus_trap_when_removed_while_open()
    {
        // arrange
        using var ctx = new BunitContext();
        var cut = await RenderOpenAsync(ctx);

        // act
        await cut.Instance.DisposeAsync();

        // assert: otherwise the next page keeps the locked scroll
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation =>
                invocation.Identifier == "Spillgebees.DocsSdk.setScrollLock"
                && Equals(invocation.Arguments[0], "search")
                && Equals(invocation.Arguments[1], false)
            );
        ctx.JSInterop.Invocations.Should()
            .Contain(invocation =>
                invocation.Identifier == "Spillgebees.DocsSdk.releaseFocusTrap"
                && Equals(invocation.Arguments[0], "search")
            );
    }
}
