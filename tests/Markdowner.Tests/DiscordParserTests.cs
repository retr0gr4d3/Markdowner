using Markdowner.Discord;
using Xunit;

namespace Markdowner.Tests;

/// <summary>
/// These pin the places where Discord deliberately disagrees with CommonMark.
/// Getting any of them wrong would make the preview quietly lie.
/// </summary>
public class DiscordParserTests
{
    private static List<DInline> Inlines(string source)
    {
        var blocks = DiscordParser.Parse(source);
        return Assert.IsType<DParagraph>(Assert.Single(blocks)).Inlines;
    }

    private static T SingleInline<T>(string source) where T : DInline =>
        Assert.IsType<T>(Assert.Single(Inlines(source)));

    [Fact]
    public void DoubleUnderscore_IsUnderline_NotBold()
    {
        var emphasis = SingleInline<DEmphasis>("__text__");
        Assert.Equal(DEmphasisKind.Underline, emphasis.Kind);
    }

    [Fact]
    public void DoubleAsterisk_IsBold()
    {
        var emphasis = SingleInline<DEmphasis>("**text**");
        Assert.Equal(DEmphasisKind.Bold, emphasis.Kind);
    }

    [Fact]
    public void SingleAsterisk_IsItalic()
    {
        var emphasis = SingleInline<DEmphasis>("*text*");
        Assert.Equal(DEmphasisKind.Italic, emphasis.Kind);
    }

    [Fact]
    public void TripleAsterisk_IsBoldItalic()
    {
        var outer = SingleInline<DEmphasis>("***text***");
        Assert.Equal(DEmphasisKind.Bold, outer.Kind);

        var inner = Assert.IsType<DEmphasis>(Assert.Single(outer.Children));
        Assert.Equal(DEmphasisKind.Italic, inner.Kind);
    }

    [Fact]
    public void Spoiler_IsParsed()
    {
        var emphasis = SingleInline<DEmphasis>("||hidden||");
        Assert.Equal(DEmphasisKind.Spoiler, emphasis.Kind);
    }

    [Fact]
    public void UnderscoresInsideWords_AreLiteral()
    {
        // Underscore emphasis requires word boundaries, so identifiers survive.
        var text = SingleInline<DText>("snake_case_name");
        Assert.Equal("snake_case_name", text.Text);
    }

    [Fact]
    public void BackslashEscape_SuppressesEmphasis()
    {
        var text = SingleInline<DText>(@"\*not italic\*");
        Assert.Equal("*not italic*", text.Text);
    }

    [Fact]
    public void CodeSpan_KeepsContentLiteral()
    {
        var code = SingleInline<DCodeSpan>("`**not bold**`");
        Assert.Equal("**not bold**", code.Code);
    }

    [Fact]
    public void SingleNewline_IsAHardBreak()
    {
        // Discord does not reflow paragraphs the way CommonMark does.
        var inlines = Inlines("first\nsecond");
        Assert.Collection(inlines,
            i => Assert.Equal("first", Assert.IsType<DText>(i).Text),
            i => Assert.IsType<DLineBreak>(i),
            i => Assert.Equal("second", Assert.IsType<DText>(i).Text));
    }

    [Theory]
    [InlineData("# One", 1)]
    [InlineData("## Two", 2)]
    [InlineData("### Three", 3)]
    public void Headings_StopAtLevelThree(string source, int level)
    {
        var heading = Assert.IsType<DHeading>(Assert.Single(DiscordParser.Parse(source)));
        Assert.Equal(level, heading.Level);
    }

    [Fact]
    public void FourHashes_IsNotAHeading()
    {
        Assert.IsType<DParagraph>(Assert.Single(DiscordParser.Parse("#### Four")));
    }

    [Fact]
    public void HashWithoutSpace_IsNotAHeading()
    {
        Assert.IsType<DParagraph>(Assert.Single(DiscordParser.Parse("#hashtag")));
    }

    [Fact]
    public void Subtext_IsParsed()
    {
        Assert.IsType<DSubtext>(Assert.Single(DiscordParser.Parse("-# small print")));
    }

    [Fact]
    public void FencedCode_CapturesLanguageAndBody()
    {
        var block = Assert.IsType<DCodeBlock>(
            Assert.Single(DiscordParser.Parse("```js\nconsole.log(1);\n```")));

        Assert.Equal("js", block.Language);
        Assert.Equal("console.log(1);", block.Code);
    }

    [Fact]
    public void UnterminatedFence_FallsBackToText()
    {
        Assert.IsType<DParagraph>(Assert.Single(DiscordParser.Parse("```js\nconsole.log(1);")));
    }

    [Fact]
    public void TripleAngleQuote_SwallowsEverythingAfterIt()
    {
        var quote = Assert.IsType<DQuote>(Assert.Single(DiscordParser.Parse(">>> quoted\nalso quoted")));
        var paragraph = Assert.IsType<DParagraph>(Assert.Single(quote.Children));

        Assert.Equal(3, paragraph.Inlines.Count); // "quoted", break, "also quoted"
    }

    [Fact]
    public void SingleAngleQuotes_MergeIntoOneBlock()
    {
        var blocks = DiscordParser.Parse("> one\n> two\nafter");

        Assert.Collection(blocks,
            b => Assert.IsType<DQuote>(b),
            b => Assert.IsType<DParagraph>(b));
    }

    [Fact]
    public void NestedList_TracksIndentLevel()
    {
        var list = Assert.IsType<DList>(Assert.Single(DiscordParser.Parse("- one\n  - two\n    - three")));

        Assert.Collection(list.Items,
            i => Assert.Equal(0, i.Level),
            i => Assert.Equal(1, i.Level),
            i => Assert.Equal(2, i.Level));
    }

    [Theory]
    [InlineData("<@123456789012345678>", DMentionKind.User)]
    [InlineData("<@&123456789012345678>", DMentionKind.Role)]
    [InlineData("<#123456789012345678>", DMentionKind.Channel)]
    [InlineData("@everyone", DMentionKind.Everyone)]
    [InlineData("@here", DMentionKind.Here)]
    [InlineData("</deploy:123456789012345678>", DMentionKind.SlashCommand)]
    public void Mentions_AreRecognised(string source, DMentionKind kind)
    {
        Assert.Equal(kind, SingleInline<DMention>(source).Kind);
    }

    [Fact]
    public void Timestamp_KeepsStyleFlag()
    {
        var timestamp = SingleInline<DTimestamp>("<t:1735689600:R>");
        Assert.Equal(1735689600, timestamp.UnixSeconds);
        Assert.Equal('R', timestamp.Style);
    }

    [Fact]
    public void CustomEmoji_IsDistinguishedFromShortcode()
    {
        Assert.True(SingleInline<DEmoji>("<a:party:123456789012345678>").Animated);
        Assert.True(SingleInline<DEmoji>("<:party:123456789012345678>").Custom);
        Assert.False(SingleInline<DEmoji>(":tada:").Custom);
    }

    [Fact]
    public void ClockTime_IsNotMistakenForAShortcode()
    {
        // ":30:" has no letters, so it must not parse as an emoji shortcode.
        var text = SingleInline<DText>("12:30:45");
        Assert.Equal("12:30:45", text.Text);
    }

    [Fact]
    public void MaskedLink_KeepsLabelAndUrl()
    {
        var link = SingleInline<DLink>("[label](https://example.com)");

        Assert.Equal("https://example.com", link.Url);
        Assert.Equal("label", Assert.IsType<DText>(Assert.Single(link.Children)).Text);
    }

    [Fact]
    public void AngleBracketedUrl_SuppressesTheEmbed()
    {
        Assert.True(SingleInline<DLink>("<https://example.com>").SuppressedEmbed);
    }

    [Fact]
    public void BareUrl_Autolinks_WithoutTrailingPunctuation()
    {
        var inlines = Inlines("see https://example.com.");
        var link = Assert.IsType<DLink>(inlines[1]);

        Assert.Equal("https://example.com", link.Url);
    }
}
