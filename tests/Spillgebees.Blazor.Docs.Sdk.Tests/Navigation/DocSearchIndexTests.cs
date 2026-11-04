using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Navigation;

public class DocSearchIndexTests
{
    private static IReadOnlyList<DocSearchEntry> CreateEntries() =>
        [
            new("Installation", "installation", "Getting Started"),
            new("ExampleView", "components/example-view", "Components"),
            new("ExampleMode", "api/ExampleMode", "API Reference", DocSearchEntryKind.Type),
            new(
                "DefaultMode",
                "api/ExampleView#DefaultMode",
                "Members",
                DocSearchEntryKind.Member,
                "Which pane is active on first render."
            ),
        ];

    [Test]
    public void Should_return_pages_when_the_query_is_empty()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "");

        // assert: a blank palette is less useful than a starting point
        results.Should().OnlyContain(entry => entry.Kind == DocSearchEntryKind.Page);
        results.Should().HaveCount(2);
    }

    [Test]
    public void Should_rank_an_exact_title_match_first()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "ExampleMode");

        // assert
        results[0].Title.Should().Be("ExampleMode");
    }

    [Test]
    public void Should_rank_a_prefix_match_above_a_substring_match()
    {
        // arrange
        IReadOnlyList<DocSearchEntry> entries = [new("MyExampleThing", "a", "G"), new("ExampleView", "b", "G")];

        // act
        var results = DocSearchIndex.Search(entries, "Example");

        // assert
        results[0].Title.Should().Be("ExampleView");
    }

    [Test]
    public void Should_match_the_description_at_a_lower_rank_than_the_title()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "pane");

        // assert
        results.Should().ContainSingle();
        results[0].Title.Should().Be("DefaultMode");
    }

    [Test]
    public void Should_be_case_insensitive()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "exampleview");

        // assert
        results.Should().Contain(entry => entry.Title == "ExampleView");
    }

    [Test]
    public void Should_return_nothing_when_there_is_no_match()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "zzz-not-here");

        // assert
        results.Should().BeEmpty();
    }

    [Test]
    public void Should_respect_the_limit()
    {
        // act
        var results = DocSearchIndex.Search(CreateEntries(), "e", limit: 2);

        // assert
        results.Should().HaveCount(2);
    }

    [Test]
    public void Should_keep_ties_in_navigation_order()
    {
        // arrange: both are substring matches, so order must be stable
        IReadOnlyList<DocSearchEntry> entries = [new("AlphaThing", "a", "G"), new("BetaThing", "b", "G")];

        // act
        var results = DocSearchIndex.Search(entries, "Thing");

        // assert
        results.Select(entry => entry.Title).Should().Equal("AlphaThing", "BetaThing");
    }

    [Test]
    public void Should_build_entries_from_navigation()
    {
        // arrange
        IReadOnlyList<NavSection> navigation =
        [
            new("Rapid start", [new NavPage("Install", "install")]),
            new("API Reference", [new NavPage("DocSite", "api/DocSite", DocSearchEntryKind.Type)]),
        ];

        // act
        var entries = DocSearchEntry.FromNavigation(navigation);

        // assert: the kind comes from the page, not from a section title that
        // happens to contain "api"
        entries.Should().HaveCount(2);
        entries[0].Kind.Should().Be(DocSearchEntryKind.Page);
        entries[1].Kind.Should().Be(DocSearchEntryKind.Type);
        entries[1].Href.Should().Be("api/DocSite");
    }

    [Test]
    public void Should_split_the_title_on_the_match_for_highlighting()
    {
        // act
        var (before, match, after) = DocSearchIndex.SplitOnMatch("DefaultMode", "mode");

        // assert: the match keeps the casing from the title, not the query
        before.Should().Be("Default");
        match.Should().Be("Mode");
        after.Should().BeEmpty();
    }

    [Test]
    public void Should_return_the_whole_string_when_the_term_is_absent()
    {
        // act
        var (before, match, after) = DocSearchIndex.SplitOnMatch("DefaultMode", "zzz");

        // assert
        before.Should().Be("DefaultMode");
        match.Should().BeEmpty();
        after.Should().BeEmpty();
    }
}
