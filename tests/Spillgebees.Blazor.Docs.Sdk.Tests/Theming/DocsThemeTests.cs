using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Theming;

public class DocsThemeTests
{
    [Test]
    public void Should_expose_refined_as_the_default_built_in()
    {
        // assert
        DocsTheme.BuiltIn.Should().StartWith([DocsTheme.Refined]);
        DocsTheme.Refined.Name.Should().Be("refined");
    }

    [Test]
    public void Should_expose_terminal_as_a_built_in()
    {
        // assert
        DocsTheme.BuiltIn.Should().Contain(DocsTheme.Terminal);
        DocsTheme.Terminal.Name.Should().Be("terminal");
    }

    [Test]
    public void Should_expose_glory_to_mankind_as_a_built_in()
    {
        // assert
        DocsTheme.BuiltIn.Should().Contain(DocsTheme.GloryToMankind);
        DocsTheme.GloryToMankind.Name.Should().Be("glory-to-mankind");
    }

    [Test]
    [Arguments("acme")]
    [Arguments("acme-dark")]
    [Arguments("brand2")]
    public void Should_accept_valid_custom_names(string name)
    {
        // act
        var theme = DocsTheme.Custom(name);

        // assert
        theme.Name.Should().Be(name);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("Acme")]
    [Arguments("acme theme")]
    [Arguments("acme_theme")]
    [Arguments("-acme")]
    [Arguments("acme-")]
    [Arguments("acme\"]{color:red}")]
    public void Should_reject_names_that_would_break_the_attribute_selector(string name)
    {
        // act
        var act = () => DocsTheme.Custom(name);

        // assert
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Should_compare_by_name()
    {
        // act
        var first = DocsTheme.Custom("acme");
        var second = DocsTheme.Custom("acme");

        // assert
        first.Should().Be(second);
        (first == second).Should().BeTrue();
        (first != DocsTheme.Refined).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Test]
    public void Should_stringify_to_the_attribute_value()
    {
        // assert
        DocsTheme.Terminal.ToString().Should().Be("terminal");
    }
}

public class DocsColorSchemeTests
{
    [Test]
    [Arguments(DocsColorScheme.System, "system")]
    [Arguments(DocsColorScheme.Light, "light")]
    [Arguments(DocsColorScheme.Dark, "dark")]
    public void Should_convert_to_the_js_wire_value(DocsColorScheme scheme, string expected)
    {
        // assert
        scheme.ToJsValue().Should().Be(expected);
    }

    [Test]
    [Arguments("light", DocsColorScheme.Light)]
    [Arguments("dark", DocsColorScheme.Dark)]
    [Arguments("system", DocsColorScheme.System)]
    [Arguments("nonsense", DocsColorScheme.System)]
    [Arguments(null, DocsColorScheme.System)]
    public void Should_parse_the_js_wire_value(string? value, DocsColorScheme expected)
    {
        // assert
        DocsColorSchemeExtensions.ParseColorScheme(value).Should().Be(expected);
    }

    [Test]
    public void Should_give_built_in_themes_a_readable_display_name()
    {
        // assert
        DocsTheme.Refined.DisplayName.Should().Be("Refined");
        DocsTheme.Terminal.DisplayName.Should().Be("Terminal");
        DocsTheme.GloryToMankind.DisplayName.Should().Be("Glory to Mankind");
    }

    [Test]
    [Arguments("acme", null, "Acme")]
    [Arguments("acme-dark", null, "Acme dark")]
    [Arguments("acme-dark", "Acme After Dark", "Acme After Dark")]
    public void Should_derive_or_accept_a_custom_display_name(string name, string? displayName, string expected)
    {
        // act
        var theme = DocsTheme.Custom(name, displayName);

        // assert
        theme.DisplayName.Should().Be(expected);
    }
}
