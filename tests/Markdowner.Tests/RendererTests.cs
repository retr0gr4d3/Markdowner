using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Markdowner.Rendering;
using Xunit;

namespace Markdowner.Tests;

/// <summary>
/// The renderers only *build* control trees — they never measure or draw — but
/// constructing a control is itself dispatcher-affine, so these run on the
/// headless UI thread rather than on an xUnit worker.
/// </summary>
public class RendererTests
{
    private static Control RenderGitHub(string markdown) => new GitHubRenderer().Render(markdown);

    private static Control RenderDiscord(string markdown) => new DiscordRenderer().Render(markdown);

    // ------------------------------------------------------------- helpers

    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;

        var children = root switch
        {
            Panel panel => panel.Children.AsEnumerable(),
            Decorator decorator => Only(decorator.Child),
            HeaderedContentControl headered => Only(headered.Header as Control).Concat(Only(headered.Content as Control)),
            ContentControl content => Only(content.Content as Control),
            _ => [],
        };

        foreach (var child in children)
        {
            foreach (var descendant in Descendants(child)) yield return descendant;
        }

        static IEnumerable<Control> Only(Control? control) =>
            control is null ? [] : [control];
    }

    private static string TextOf(Control root) =>
        string.Join("\n", Descendants(root).OfType<TextBlock>().Select(BlockText));

    private static string BlockText(TextBlock block) =>
        block.Inlines is { Count: > 0 } inlines
            ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
            : block.Text ?? string.Empty;

    private static IEnumerable<MarkdownTextBlock> RichBlocks(Control root) =>
        Descendants(root).OfType<MarkdownTextBlock>();

    // ---------------------------------------------------------- code blocks

    [AvaloniaFact]
    public void Discord_FencedCode_RendersItsBody()
    {
        // Regression: the code text was being laid out to zero size and vanishing.
        var rendered = RenderDiscord("```js\nconsole.log(1);\n```");
        Assert.Contains("console.log(1);", TextOf(rendered));
    }

    [AvaloniaFact]
    public void GitHub_FencedCode_RendersBodyAndLanguageLabel()
    {
        var text = TextOf(RenderGitHub("```csharp\nvar x = 1;\n```"));

        Assert.Contains("var x = 1;", text);
        Assert.Contains("csharp", text);
    }

    [AvaloniaFact]
    public void GitHub_MermaidFence_IsCalledOut()
    {
        Assert.Contains("Mermaid", TextOf(RenderGitHub("```mermaid\ngraph TD;\n```")));
    }

    // ----------------------------------------------------------------- HTML

    [AvaloniaFact]
    public void GitHub_Details_BecomesAnExpander()
    {
        var rendered = RenderGitHub("<details>\n<summary>Click to expand</summary>\n\nBody text.\n\n</details>");
        var expander = Assert.Single(Descendants(rendered).OfType<Expander>());

        Assert.Contains("Click to expand", TextOf(expander));
    }

    [AvaloniaFact]
    public void GitHub_DisallowedTag_IsReportedAsStripped()
    {
        // The preview must say what github.com would remove, not render it.
        Assert.Contains("removed by GitHub", TextOf(RenderGitHub("<script>alert(1)</script>")));
    }

    [AvaloniaFact]
    public void GitHub_AllowedInlineHtml_KeepsItsText()
    {
        Assert.Contains("Ctrl", TextOf(RenderGitHub("Press <kbd>Ctrl</kbd> now.")));
    }

    [AvaloniaFact]
    public void GitHub_HtmlTable_BuildsCells()
    {
        var rendered = RenderGitHub("<table><tr><th>A</th><td>B</td></tr></table>");
        var text = TextOf(rendered);

        Assert.Contains("A", text);
        Assert.Contains("B", text);
    }

    // --------------------------------------------------------------- blocks

    [AvaloniaFact]
    public void GitHub_PipeTable_BuildsAGrid()
    {
        var rendered = RenderGitHub("| A | B |\n| --- | --- |\n| 1 | 2 |");
        var grid = Descendants(rendered).OfType<Grid>().FirstOrDefault(g => g.RowDefinitions.Count == 2);

        Assert.NotNull(grid);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
    }

    [AvaloniaFact]
    public void GitHub_TaskList_PutsTheCheckboxInTheMarkerGutter()
    {
        var text = TextOf(RenderGitHub("- [x] done\n- [ ] todo"));

        Assert.Contains("☑", text);
        Assert.Contains("☐", text);
    }

    [AvaloniaFact]
    public void GitHub_Alert_ShowsItsKind()
    {
        Assert.Contains("Warning", TextOf(RenderGitHub("> [!WARNING]\n> Careful.")));
    }

    [AvaloniaFact]
    public void GitHub_Footnote_RendersTheSection()
    {
        Assert.Contains("Footnotes", TextOf(RenderGitHub("Text.[^1]\n\n[^1]: The note.")));
    }

    [AvaloniaFact]
    public void GitHub_Link_IsRecordedAsAClickableRange()
    {
        var rendered = RenderGitHub("[label](https://example.com)");
        var link = RichBlocks(rendered).SelectMany(b => b.Links).Single();

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal(5, link.Length); // "label"
    }

    // -------------------------------------------------------------- Discord

    [AvaloniaFact]
    public void Discord_Spoiler_IsHiddenUntilRevealed()
    {
        var rendered = RenderDiscord("a ||secret|| b");
        var spoiler = Assert.Single(RichBlocks(rendered).SelectMany(b => b.Spoilers));

        Assert.False(spoiler.IsRevealed);
        spoiler.Reveal();
        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void Discord_Mention_RendersAsAChip()
    {
        Assert.Contains("@user", TextOf(RenderDiscord("hi <@123456789012345678>")));
    }

    [AvaloniaFact]
    public void Discord_Shortcode_BecomesAnEmoji()
    {
        Assert.Contains("🎉", TextOf(RenderDiscord(":tada:")));
    }

    [AvaloniaFact]
    public void Discord_Subtext_Renders()
    {
        Assert.Contains("small print", TextOf(RenderDiscord("-# small print")));
    }

    [AvaloniaFact]
    public void BothRenderers_HandleTheSharedSampleDocument()
    {
        // The starter document is deliberately one file covering both dialects,
        // so either renderer must cope with the other's syntax without throwing.
        Assert.NotNull(RenderGitHub(Models.SampleDocuments.Default));
        Assert.NotNull(RenderDiscord(Models.SampleDocuments.Default));
    }

    [AvaloniaTheory]
    [InlineData(Models.MarkdownFlavor.GitHub)]
    [InlineData(Models.MarkdownFlavor.Discord)]
    public void EveryFormattingHelpExample_Renders(Models.MarkdownFlavor flavor)
    {
        // The help window renders each entry live, so a bad example would show
        // up there as an error card rather than as documentation.
        var renderer = flavor == Models.MarkdownFlavor.Discord
            ? (IMarkdownRenderer)new DiscordRenderer()
            : new GitHubRenderer();

        foreach (var section in Models.FormattingReference.For(flavor))
        {
            foreach (var entry in section.Entries)
            {
                Assert.NotNull(renderer.Render(entry.Syntax));
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("```unterminated")]
    [InlineData("| broken |")]
    [InlineData("<div><span>unclosed")]
    [InlineData("[link](")]
    [InlineData("||")]
    public void Renderers_TolerateMalformedInput(string markdown)
    {
        Assert.NotNull(RenderGitHub(markdown));
        Assert.NotNull(RenderDiscord(markdown));
    }
}
