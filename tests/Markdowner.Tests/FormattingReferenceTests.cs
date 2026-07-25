using Markdowner.Models;
using Xunit;

namespace Markdowner.Tests;

public class FormattingReferenceTests
{
    private static IEnumerable<FormatEntry> All(MarkdownFlavor flavor) =>
        FormattingReference.For(flavor).SelectMany(section => section.Entries);

    [Theory]
    [InlineData(MarkdownFlavor.GitHub)]
    [InlineData(MarkdownFlavor.Discord)]
    public void EveryEntry_IsWellFormed(MarkdownFlavor flavor)
    {
        foreach (var entry in All(flavor))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Name));
            Assert.False(string.IsNullOrWhiteSpace(entry.Syntax));
        }
    }

    [Theory]
    [InlineData(MarkdownFlavor.GitHub)]
    [InlineData(MarkdownFlavor.Discord)]
    public void SectionNames_AreUnique(MarkdownFlavor flavor)
    {
        var names = FormattingReference.For(flavor).Select(s => s.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void GitHub_DocumentsItsExclusiveFeatures()
    {
        var syntax = string.Join("\n", All(MarkdownFlavor.GitHub).Select(e => e.Syntax));

        Assert.Contains("<details>", syntax);
        Assert.Contains("<kbd>", syntax);
        Assert.Contains("[!NOTE]", syntax);
        Assert.Contains("[^ref]", syntax);
        Assert.Contains("| --- |", syntax);
        Assert.Contains("- [x]", syntax);
    }

    [Fact]
    public void Discord_DocumentsItsExclusiveFeatures()
    {
        var syntax = string.Join("\n", All(MarkdownFlavor.Discord).Select(e => e.Syntax));

        Assert.Contains("||", syntax);          // spoiler
        Assert.Contains("-# ", syntax);         // subtext
        Assert.Contains(">>>", syntax);         // block quote
        Assert.Contains("<t:1735689600:R>", syntax);
        Assert.Contains("<@&", syntax);         // role mention
    }

    [Fact]
    public void Discord_DocumentsAllSevenTimestampStyles()
    {
        var timestamps = FormattingReference.For(MarkdownFlavor.Discord)
            .Single(section => section.Name == "Timestamps");

        Assert.Equal(7, timestamps.Entries.Count);

        foreach (var style in "tTdDfFR")
        {
            Assert.Contains(timestamps.Entries, e => e.Syntax.Contains($":{style}>"));
        }
    }

    [Fact]
    public void Discord_DoesNotClaimToSupportHtmlOrTables()
    {
        // The Discord reference may *mention* these, but only to say they are
        // unsupported — that section is named accordingly.
        var section = FormattingReference.For(MarkdownFlavor.Discord)
            .Single(s => s.Entries.Any(e => e.Syntax.Contains("<kbd>")));

        Assert.Equal("Not supported", section.Name);
    }
}
