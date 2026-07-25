using Markdowner.Html;
using Markdowner.Models;
using Xunit;

namespace Markdowner.Tests;

public class HtmlParserTests
{
    private static HtmlElement FirstElement(string html) =>
        Assert.IsType<HtmlElement>(HtmlParser.Parse(html).First(n => n is HtmlElement));

    [Fact]
    public void ParsesNestedElementsAndText()
    {
        var root = FirstElement("<div><b>bold</b> tail</div>");

        Assert.Equal("div", root.Tag);
        Assert.Equal("b", Assert.IsType<HtmlElement>(root.Children[0]).Tag);
        Assert.Equal(" tail", Assert.IsType<HtmlText>(root.Children[1]).Text);
    }

    [Theory]
    [InlineData("<a href=\"https://example.com\">x</a>")]
    [InlineData("<a href='https://example.com'>x</a>")]
    [InlineData("<a href=https://example.com>x</a>")]
    public void ParsesAttributes_InEveryQuotingStyle(string html)
    {
        Assert.Equal("https://example.com", FirstElement(html).Attribute("href"));
    }

    [Fact]
    public void VoidElements_DoNotSwallowFollowingContent()
    {
        var nodes = HtmlParser.Parse("<p>a<br>b</p>");
        var paragraph = Assert.IsType<HtmlElement>(nodes[0]);

        Assert.Equal(3, paragraph.Children.Count);
        Assert.Empty(Assert.IsType<HtmlElement>(paragraph.Children[1]).Children);
    }

    [Fact]
    public void UnclosedTags_CloseAtTheirParent()
    {
        var root = FirstElement("<div><span>text</div>");

        Assert.Equal("div", root.Tag);
        Assert.Equal("span", Assert.IsType<HtmlElement>(Assert.Single(root.Children)).Tag);
    }

    [Fact]
    public void StrayClosingTag_IsIgnored()
    {
        var nodes = HtmlParser.Parse("text</div>more");
        Assert.All(nodes, node => Assert.IsType<HtmlText>(node));
    }

    [Fact]
    public void Comments_AreDropped()
    {
        Assert.Empty(HtmlParser.Parse("<!-- hidden -->"));
    }

    [Fact]
    public void ScriptBody_IsNotParsedAsMarkup()
    {
        // The "<b>" inside the script must not become an element.
        var nodes = HtmlParser.Parse("<script>if (a < b) { }</script><p>after</p>");
        var elements = nodes.OfType<HtmlElement>().ToList();

        Assert.Equal("script", elements[0].Tag);
        Assert.Empty(elements[0].Children);
        Assert.Equal("p", elements[1].Tag);
    }

    [Fact]
    public void Entities_AreDecoded()
    {
        var text = Assert.IsType<HtmlText>(HtmlParser.Parse("a &amp; b &lt;c&gt;")[0]);
        Assert.Equal("a & b <c>", text.Text);
    }

    [Fact]
    public void ListItems_ImplicitlyCloseEachOther()
    {
        var list = FirstElement("<ul><li>one<li>two</ul>");

        Assert.Equal(2, list.Children.OfType<HtmlElement>().Count());
    }

    [Fact]
    public void TableRows_ImplicitlyCloseEachOther()
    {
        var table = FirstElement("<table><tr><td>a<td>b<tr><td>c</table>");
        var rows = table.Children.OfType<HtmlElement>().Where(e => e.Tag == "tr").ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].Children.OfType<HtmlElement>().Count());
    }
}

public class HtmlSpecTests
{
    [Theory]
    [InlineData("kbd")]
    [InlineData("details")]
    [InlineData("summary")]
    [InlineData("sub")]
    [InlineData("sup")]
    [InlineData("picture")]
    [InlineData("video")]
    public void AllowList_CoversTagsGitHubRenders(string tag) =>
        Assert.Contains(tag, HtmlSpec.Allowed);

    [Theory]
    [InlineData("script")]
    [InlineData("style")]
    [InlineData("iframe")]
    [InlineData("form")]
    [InlineData("button")]
    public void StrippedSet_CoversTagsGitHubRemoves(string tag)
    {
        Assert.Contains(tag, HtmlSpec.Stripped);
        Assert.DoesNotContain(tag, HtmlSpec.Allowed);
    }

    [Fact]
    public void AllowedAndStripped_DoNotOverlap() =>
        Assert.Empty(HtmlSpec.Allowed.Intersect(HtmlSpec.Stripped));
}

public class SnippetLibraryTests
{
    private static IEnumerable<Snippet> All(MarkdownFlavor flavor) =>
        SnippetLibrary.For(flavor).SelectMany(c => c.Groups).SelectMany(g => g.Snippets);

    [Fact]
    public void GitHub_OffersRawHtmlTags()
    {
        var categories = SnippetLibrary.For(MarkdownFlavor.GitHub);
        Assert.Contains(categories, c => c.Name == "HTML");
    }

    [Fact]
    public void Discord_DoesNotOfferHtml()
    {
        // Discord strips HTML entirely, so offering the tags would be a trap.
        var categories = SnippetLibrary.For(MarkdownFlavor.Discord);
        Assert.DoesNotContain(categories, c => c.Name == "HTML");
    }

    [Fact]
    public void EveryHtmlSnippet_UsesAnAllowListedTag()
    {
        var html = SnippetLibrary.For(MarkdownFlavor.GitHub).Single(c => c.Name == "HTML");

        foreach (var snippet in html.Groups.SelectMany(g => g.Snippets))
        {
            var tag = snippet.Label.Trim('<', '>', '/');
            Assert.Contains(tag, HtmlSpec.Allowed);
        }
    }

    [Fact]
    public void Discord_OffersItsExclusiveSyntax()
    {
        var labels = All(MarkdownFlavor.Discord).Select(s => s.Tooltip).ToList();

        Assert.Contains(labels, t => t.Contains("Spoiler"));
        Assert.Contains(labels, t => t.Contains("Underline"));
        Assert.Contains(labels, t => t.Contains("Subtext"));
    }

    [Theory]
    [InlineData(MarkdownFlavor.GitHub)]
    [InlineData(MarkdownFlavor.Discord)]
    public void EverySnippet_IsWellFormed(MarkdownFlavor flavor)
    {
        foreach (var snippet in All(flavor))
        {
            Assert.False(string.IsNullOrWhiteSpace(snippet.Label));
            Assert.False(string.IsNullOrWhiteSpace(snippet.Tooltip));
            Assert.False(string.IsNullOrEmpty(snippet.Before));

            if (snippet.Kind == SnippetKind.Wrap)
            {
                Assert.False(string.IsNullOrEmpty(snippet.After));
                Assert.False(string.IsNullOrEmpty(snippet.Placeholder));
            }
        }
    }

    [Theory]
    [InlineData(MarkdownFlavor.GitHub)]
    [InlineData(MarkdownFlavor.Discord)]
    public void BlockSnippets_PlaceholdersActuallyAppearInTheirText(MarkdownFlavor flavor)
    {
        // The applier selects the placeholder inside inserted text; a typo here
        // would silently leave the caret in the wrong place.
        foreach (var snippet in All(flavor))
        {
            if (snippet.Kind is not (SnippetKind.Block or SnippetKind.Insert)) continue;
            if (snippet.Placeholder.Length == 0) continue;

            Assert.Contains(snippet.Placeholder, snippet.Before);
        }
    }
}
