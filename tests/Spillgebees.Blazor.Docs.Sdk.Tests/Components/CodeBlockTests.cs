using AwesomeAssertions;
using Spillgebees.Blazor.Docs.Sdk;

namespace Spillgebees.Blazor.Docs.Sdk.Tests.Components;

public class CodeBlockTests
{
    [Test]
    public void Should_render_single_file_as_plain_text()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Program.cs"] = "var x = 1;" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));

        // assert
        var code = cut.Find("pre code");
        code.TextContent.Should().Be("var x = 1;");
    }

    [Test]
    public void Should_render_tabs_for_multiple_files()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Program.cs"] = "hello", ["Startup.cs"] = "world" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));

        // assert
        var tabs = cut.FindAll(".docs-code-tab");
        tabs.Should().HaveCount(2);
        tabs[0].TextContent.Should().Be("Program.cs");
        tabs[1].TextContent.Should().Be("Startup.cs");
    }

    [Test]
    public void Should_render_copy_button()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Program.cs"] = "hello" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));

        // assert
        var copyButton = cut.Find(".docs-code-copy");
        copyButton.Should().NotBeNull();
    }

    [Test]
    public void Should_render_bar_with_tabs_and_copy()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Program.cs"] = "hello", ["Startup.cs"] = "world" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));

        // assert
        var bar = cut.Find(".docs-code-bar");
        bar.Should().NotBeNull();
        var copy = cut.Find(".docs-code-copy");
        copy.Should().NotBeNull();
    }

    [Test]
    public void Should_infer_csharp_language_for_code_behind_file()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Counter.razor.cs"] = "public partial class Counter {}" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, files).Add(p => p.Language, "razor")
        );

        // assert
        var code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-csharp");
    }

    [Test]
    public void Should_infer_razor_language_for_razor_file()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Counter.razor"] = "<h1>Hello</h1>" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, files).Add(p => p.Language, "razor")
        );

        // assert
        var code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-cshtml-razor");
    }

    [Test]
    public void Should_switch_language_when_changing_active_tab_between_razor_and_code_behind()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string>
        {
            ["Counter.razor"] = "<h1>Hello</h1>",
            ["Counter.razor.cs"] = "public partial class Counter {}",
        };

        // act
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, files).Add(p => p.Language, "razor")
        );

        // assert - first tab is razor
        var code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-cshtml-razor");

        // act - click the code-behind tab
        var tabs = cut.FindAll(".docs-code-tab");
        tabs[1].Click();

        // assert - should now be csharp
        code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-csharp");
    }

    [Test]
    public void Should_infer_language_from_cs_extension()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["Program.cs"] = "var x = 1;" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));

        // assert
        var code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-csharp");
    }

    [Test]
    public void Should_fall_back_to_language_parameter_for_unknown_extension()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.bindSelect", _ => true);
        var files = new Dictionary<string, string> { ["script.py"] = "print('hello')" };

        // act
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, files).Add(p => p.Language, "python")
        );

        // assert
        var code = cut.Find("pre code");
        code.ClassList.Should().Contain("language-python");
    }

    [Test]
    public void Should_not_rehighlight_when_copying()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.highlightElement", _ => true);
        ctx.JSInterop.SetupVoid("Spillgebees.DocsSdk.fitCodeTabs", _ => true).SetVoidResult();
        var files = new Dictionary<string, string> { ["Program.cs"] = "var x = 1;" };
        var cut = ctx.Render<CodeBlock>(parameters => parameters.Add(p => p.Files, files));
        var highlightsBefore = ctx.JSInterop.Invocations["Spillgebees.DocsSdk.highlightElement"].Count;

        // act: highlighting rebuilds the code DOM, which would clear a selection
        cut.Find(".docs-code-copy").Click();

        // assert
        ctx.JSInterop.Invocations["Spillgebees.DocsSdk.highlightElement"].Count.Should().Be(highlightsBefore);
    }

    [Test]
    public void Should_rehighlight_when_only_the_language_changes()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var files = new Dictionary<string, string> { ["snippet"] = "{ \"value\": true }" };
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, files).Add(p => p.Language, "csharp")
        );
        var highlightsBefore = ctx.JSInterop.Invocations["Spillgebees.DocsSdk.highlightElement"].Count;

        // act
        cut.Render(parameters => parameters.Add(p => p.Files, files).Add(p => p.Language, "json"));

        // assert
        cut.Find("pre code").ClassList.Should().Contain("language-json");
        ctx.JSInterop.Invocations["Spillgebees.DocsSdk.highlightElement"].Count.Should().Be(highlightsBefore + 1);
    }

    [Test]
    public void Should_refit_the_file_tabs_when_files_are_added()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, new Dictionary<string, string> { ["Short.cs"] = "a", ["B.cs"] = "b" })
        );
        var fitsBefore = ctx.JSInterop.Invocations["Spillgebees.DocsSdk.fitCodeTabs"].Count;

        // act: the active file is unchanged, so nothing is re-highlighted
        cut.Render(parameters =>
            parameters.Add(
                p => p.Files,
                new Dictionary<string, string>
                {
                    ["Short.cs"] = "a",
                    ["B.cs"] = "b",
                    ["AnotherReallyLongSourceFileName.cs"] = "c",
                }
            )
        );

        // assert
        ctx.JSInterop.Invocations["Spillgebees.DocsSdk.fitCodeTabs"].Count.Should().Be(fitsBefore + 1);
    }

    [Test]
    public void Should_announce_the_copy_result()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.Setup<bool>("Spillgebees.DocsSdk.copyToClipboard", _ => true).SetResult(true);
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, new Dictionary<string, string> { ["Program.cs"] = "x" })
        );
        cut.Find("[role='status']").TextContent.Should().BeEmpty();

        // act
        cut.Find(".docs-code-copy").Click();

        // assert
        cut.Find("[role='status']").TextContent.Should().Be("Copied");
    }

    [Test]
    public void Should_label_a_single_file_without_a_selectable_tab()
    {
        // arrange
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        // act
        var cut = ctx.Render<CodeBlock>(parameters =>
            parameters.Add(p => p.Files, new Dictionary<string, string> { ["Program.cs"] = "x" })
        );

        // assert
        cut.Find(".docs-code-file").TextContent.Should().Be("Program.cs");
        cut.FindAll(".docs-code-tab").Should().BeEmpty();
    }
}
