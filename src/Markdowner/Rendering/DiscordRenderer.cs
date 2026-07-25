using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdowner.Discord;

namespace Markdowner.Rendering;

/// <summary>Renders Discord's message markdown the way the Discord client shows it.</summary>
public sealed class DiscordRenderer : IMarkdownRenderer
{
    public PreviewTheme Theme => PreviewTheme.Discord;

    public Control Render(string markdown)
    {
        var stack = BlockFactory.Stack(4);
        foreach (var control in RenderBlocks(DiscordParser.Parse(markdown)))
        {
            stack.Children.Add(control);
        }
        return stack;
    }

    private IEnumerable<Control> RenderBlocks(List<DBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case DHeading heading:
                    yield return RenderHeading(heading);
                    break;

                case DSubtext subtext:
                {
                    var writer = new InlineWriter(Theme);
                    WriteInlines(subtext.Inlines, writer, new RunStyle { Foreground = Theme.Muted });
                    var text = writer.Build(fontSize: Theme.FontSize - 2.5, foreground: Theme.Muted, lineHeight: 18);
                    text.Margin = new Thickness(0, 2, 0, 0);
                    yield return text;
                    break;
                }

                case DParagraph paragraph:
                {
                    var writer = new InlineWriter(Theme);
                    WriteInlines(paragraph.Inlines, writer, default);
                    yield return writer.Build();
                    break;
                }

                case DQuote quote:
                    yield return BlockFactory.Quote(Theme, RenderBlocks(quote.Children));
                    break;

                case DCodeBlock code:
                    yield return BlockFactory.CodeBlock(Theme, code.Code, code.Language, showLanguage: false);
                    break;

                case DList list:
                    foreach (var control in RenderList(list)) yield return control;
                    break;
            }
        }
    }

    private Control RenderHeading(DHeading heading)
    {
        var size = heading.Level switch
        {
            1 => Theme.FontSize + 9,
            2 => Theme.FontSize + 5,
            _ => Theme.FontSize + 2,
        };

        var writer = new InlineWriter(Theme);
        WriteInlines(heading.Inlines, writer, new RunStyle { Bold = true, Foreground = Theme.Heading });

        var block = writer.Build(fontSize: size, foreground: Theme.Heading,
            fontWeight: FontWeight.Bold, lineHeight: size + 8);
        block.Margin = new Thickness(0, 8, 0, 0);
        return block;
    }

    private IEnumerable<Control> RenderList(DList list)
    {
        foreach (var item in list.Items)
        {
            var writer = new InlineWriter(Theme);
            WriteInlines(item.Inlines, writer, default);

            // Discord cycles bullet glyphs as lists nest.
            var marker = list.Ordered
                ? item.Marker
                : item.Level switch { 0 => "•", 1 => "◦", _ => "▪" };

            yield return BlockFactory.ListRow(Theme, marker, writer.Build(), item.Level);
        }
    }

    // ------------------------------------------------------------- inlines

    private void WriteInlines(List<DInline> inlines, InlineWriter writer, RunStyle style)
    {
        foreach (var inline in inlines) WriteInline(inline, writer, style);
    }

    private void WriteInline(DInline inline, InlineWriter writer, RunStyle style)
    {
        switch (inline)
        {
            case DText text:
                style.ApplyTo(writer.Add(text.Text));
                break;

            case DLineBreak:
                writer.LineBreak();
                break;

            case DEmphasis emphasis:
            {
                if (emphasis.Kind == DEmphasisKind.Spoiler)
                {
                    var offset = writer.Offset;
                    var index = writer.InlineCount;
                    WriteInlines(emphasis.Children, writer, style);
                    writer.MarkSpoiler(offset, index);
                    break;
                }

                var nested = emphasis.Kind switch
                {
                    DEmphasisKind.Bold => style with { Bold = true },
                    DEmphasisKind.Italic => style with { Italic = true },
                    DEmphasisKind.Underline => style with { Underline = true },
                    _ => style with { Strikethrough = true },
                };
                WriteInlines(emphasis.Children, writer, nested);
                break;
            }

            case DCodeSpan code:
            {
                var run = writer.Add(code.Code);
                (style with
                {
                    Font = Theme.MonoFont,
                    Background = Theme.CodeBackground,
                    Foreground = Theme.CodeText,
                    FontSize = Theme.FontSize - 1,
                }).ApplyTo(run);
                break;
            }

            case DLink link:
            {
                var offset = writer.Offset;
                WriteInlines(link.Children, writer, style with { Foreground = Theme.Link });
                writer.MarkLink(offset, link.Url);
                break;
            }

            case DMention mention:
            {
                var run = writer.Add(mention.Display);
                (style with
                {
                    Background = Theme.MentionBackground,
                    Foreground = Theme.MentionText,
                    Bold = true,
                }).ApplyTo(run);
                break;
            }

            case DEmoji emoji:
            {
                if (!emoji.Custom && EmojiMap.TryGet(emoji.Name, out var glyph))
                {
                    style.ApplyTo(writer.Add(glyph));
                    break;
                }

                // Custom server emoji can't be resolved offline — show the code.
                var run = writer.Add($":{emoji.Name}:");
                (style with
                {
                    Background = Theme.SubtleBackground,
                    Foreground = emoji.Animated ? Theme.Link : Theme.Muted,
                }).ApplyTo(run);
                break;
            }

            case DTimestamp timestamp:
            {
                var run = writer.Add(FormatTimestamp(timestamp));
                (style with
                {
                    Background = Theme.MentionBackground,
                    Foreground = Theme.Text,
                }).ApplyTo(run);
                break;
            }
        }
    }

    private static string FormatTimestamp(DTimestamp timestamp)
    {
        DateTimeOffset moment;
        try
        {
            moment = DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixSeconds).ToLocalTime();
        }
        catch (ArgumentOutOfRangeException)
        {
            return "Invalid Date";
        }

        return timestamp.Style switch
        {
            't' => moment.ToString("h:mm tt"),
            'T' => moment.ToString("h:mm:ss tt"),
            'd' => moment.ToString("MM/dd/yyyy"),
            'D' => moment.ToString("MMMM d, yyyy"),
            'F' => moment.ToString("dddd, MMMM d, yyyy h:mm tt"),
            'R' => Relative(moment),
            _ => moment.ToString("MMMM d, yyyy h:mm tt"),
        };
    }

    private static string Relative(DateTimeOffset moment)
    {
        var delta = moment - DateTimeOffset.Now;
        var future = delta > TimeSpan.Zero;
        var span = delta.Duration();

        var (value, unit) = span switch
        {
            { TotalDays: >= 365 } => (span.TotalDays / 365, "year"),
            { TotalDays: >= 30 } => (span.TotalDays / 30, "month"),
            { TotalDays: >= 1 } => (span.TotalDays, "day"),
            { TotalHours: >= 1 } => (span.TotalHours, "hour"),
            { TotalMinutes: >= 1 } => (span.TotalMinutes, "minute"),
            _ => (span.TotalSeconds, "second"),
        };

        var rounded = (int)Math.Round(value);
        if (rounded == 0) return "now";

        var plural = rounded == 1 ? unit : unit + "s";
        return future ? $"in {rounded} {plural}" : $"{rounded} {plural} ago";
    }
}
