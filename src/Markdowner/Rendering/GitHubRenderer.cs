using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdowner.Html;

namespace Markdowner.Rendering;

/// <summary>
/// Renders GitHub Flavored Markdown, including the raw HTML GitHub permits.
/// Parsing is delegated to Markdig with an extension set chosen to match what
/// github.com actually enables — notably *not* Markdig's extra emphasis forms,
/// which GitHub does not support.
/// </summary>
public sealed class GitHubRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseFootnotes()
        .UseAlertBlocks()
        .UseMathematics()
        .UseEmojiAndSmiley(enableSmileys: false)
        .UseAutoIdentifiers()
        .Build();

    public PreviewTheme Theme => PreviewTheme.GitHub;

    private readonly HtmlRenderer _html = new(PreviewTheme.GitHub);
    private int _listLevel;

    public Control Render(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        var stack = BlockFactory.Stack(10);
        foreach (var control in RenderBlocks(document)) stack.Children.Add(control);
        return stack;
    }

    // -------------------------------------------------------------- blocks

    private IEnumerable<Control> RenderBlocks(IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            // Order matters: AlertBlock derives from QuoteBlock, and
            // MathBlock/FencedCodeBlock both derive from CodeBlock.
            switch (block)
            {
                case AlertBlock alert:
                    yield return RenderAlert(alert);
                    break;

                case HeadingBlock heading:
                    yield return RenderHeading(heading);
                    break;

                case ParagraphBlock paragraph:
                {
                    var writer = new InlineWriter(Theme);
                    WriteInlines(paragraph.Inline, writer, default);
                    yield return writer.Build();
                    break;
                }

                case QuoteBlock quote:
                    yield return BlockFactory.Quote(Theme, RenderBlocks(quote).ToList());
                    break;

                case ListBlock list:
                    foreach (var control in RenderList(list)) yield return control;
                    break;

                case MathBlock math:
                    yield return BlockFactory.CodeBlock(Theme, LinesOf(math).Trim('\n'), "math");
                    break;

                case FencedCodeBlock fenced:
                    yield return RenderFencedCode(fenced);
                    break;

                case CodeBlock code:
                    yield return BlockFactory.CodeBlock(Theme, LinesOf(code).TrimEnd('\n'), null);
                    break;

                case ThematicBreakBlock:
                    yield return BlockFactory.Rule(Theme);
                    break;

                case Table table:
                    yield return RenderTable(table);
                    break;

                case HtmlBlock html:
                    foreach (var control in _html.Render(LinesOf(html))) yield return control;
                    break;

                case FootnoteGroup footnotes:
                    foreach (var control in RenderFootnotes(footnotes)) yield return control;
                    break;

                case LinkReferenceDefinitionGroup:
                    break;

                case ContainerBlock container:
                    foreach (var control in RenderBlocks(container)) yield return control;
                    break;
            }
        }
    }

    private Control RenderHeading(HeadingBlock heading)
    {
        var size = heading.Level switch
        {
            1 => Theme.FontSize + 11,
            2 => Theme.FontSize + 7,
            3 => Theme.FontSize + 3.5,
            4 => Theme.FontSize + 1,
            5 => Theme.FontSize - 0.5,
            _ => Theme.FontSize - 1.5,
        };

        var writer = new InlineWriter(Theme);
        WriteInlines(heading.Inline, writer, new RunStyle { Bold = true, Foreground = Theme.Heading });

        var block = writer.Build(fontSize: size,
            foreground: heading.Level >= 6 ? Theme.Muted : Theme.Heading,
            fontWeight: FontWeight.Bold,
            lineHeight: size + 9);
        block.Margin = new Thickness(0, heading.Level <= 2 ? 14 : 10, 0, 0);

        // GitHub underlines h1 and h2.
        if (heading.Level > 2) return block;

        var stack = BlockFactory.Stack(7);
        stack.Children.Add(block);
        stack.Children.Add(new Border { Height = 1, Background = Theme.Border });
        return stack;
    }

    private Control RenderFencedCode(FencedCodeBlock fenced)
    {
        var language = fenced.Info ?? string.Empty;
        var source = LinesOf(fenced).TrimEnd('\n');

        var code = BlockFactory.CodeBlock(Theme, source, language);
        if (!language.Equals("mermaid", StringComparison.OrdinalIgnoreCase)) return code;

        // GitHub turns mermaid fences into diagrams; say so rather than pretending.
        var stack = BlockFactory.Stack(6);
        stack.Children.Add(BlockFactory.Placeholder(Theme, "📊",
            "GitHub renders this block as a Mermaid diagram."));
        stack.Children.Add(code);
        return stack;
    }

    private Control RenderAlert(AlertBlock alert)
    {
        var kind = alert.Kind.ToString().ToUpperInvariant();

        var (accent, glyph, title) = kind switch
        {
            "TIP" => ("#3FB950", "💡", "Tip"),
            "IMPORTANT" => ("#AB7DF8", "❗", "Important"),
            "WARNING" => ("#D29922", "⚠️", "Warning"),
            "CAUTION" => ("#F85149", "🛑", "Caution"),
            _ => ("#4493F8", "ℹ️", "Note"),
        };

        var brush = Brush.Parse(accent);

        var header = new TextBlock
        {
            Text = $"{glyph}  {title}",
            Foreground = brush,
            FontWeight = FontWeight.Bold,
            FontSize = Theme.FontSize,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var body = new List<Control> { header };
        body.AddRange(RenderBlocks(alert));

        return BlockFactory.Quote(Theme, body, brush);
    }

    private IEnumerable<Control> RenderList(ListBlock list)
    {
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            // GitHub replaces the bullet with the checkbox on task-list items,
            // so the box belongs in the marker gutter, not in the text column.
            var task = LeadingTask(item);

            var marker = task is not null
                ? task.Checked ? "☑" : "☐"
                : list.IsOrdered
                    ? $"{number++}."
                    : _listLevel switch { 0 => "•", 1 => "◦", _ => "▪" };

            var level = _listLevel;
            _listLevel++;
            var children = RenderBlocks(item).ToList();
            _listLevel--;

            var content = children.Count == 1
                ? children[0]
                : Wrap(children);

            yield return BlockFactory.ListRow(Theme, marker, content, level);
        }
    }

    /// <summary>The task marker at the very start of an item, ignoring nested lists.</summary>
    private static TaskList? LeadingTask(ListItemBlock item) =>
        item.Count > 0 && item[0] is ParagraphBlock { Inline: { } inline }
            ? inline.FirstChild as TaskList
            : null;

    private IEnumerable<Control> RenderFootnotes(FootnoteGroup group)
    {
        yield return BlockFactory.Rule(Theme);

        yield return new TextBlock
        {
            Text = "Footnotes",
            Foreground = Theme.Heading,
            FontWeight = FontWeight.Bold,
            FontSize = Theme.FontSize + 1,
        };

        foreach (var footnote in group.OfType<Footnote>())
        {
            var children = RenderBlocks(footnote).ToList();
            yield return BlockFactory.ListRow(Theme, $"{footnote.Order}.", Wrap(children), 0);
        }
    }

    private Control RenderTable(Table table)
    {
        var rows = new List<IReadOnlyList<TableCellSpec>>();

        foreach (var row in table.OfType<TableRow>())
        {
            var cells = new List<TableCellSpec>();

            foreach (var cell in row.OfType<TableCell>())
            {
                var children = RenderBlocks(cell).ToList();
                var content = children.Count == 1 ? children[0] : Wrap(children);

                var columnIndex = cells.Count;
                if (content is TextBlock text &&
                    columnIndex < table.ColumnDefinitions.Count &&
                    table.ColumnDefinitions[columnIndex].Alignment is { } alignment)
                {
                    text.TextAlignment = alignment switch
                    {
                        TableColumnAlign.Center => TextAlignment.Center,
                        TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left,
                    };
                }

                if (row.IsHeader && content is TextBlock header) header.FontWeight = FontWeight.Bold;

                cells.Add(new TableCellSpec(content, row.IsHeader, cell.ColumnSpan, cell.RowSpan));
            }

            if (cells.Count > 0) rows.Add(cells);
        }

        return TableFactory.Build(Theme, rows);
    }

    private Control Wrap(IEnumerable<Control> controls)
    {
        var stack = BlockFactory.Stack(6);
        foreach (var control in controls) stack.Children.Add(control);
        return stack;
    }

    // ------------------------------------------------------------- inlines

    /// <summary>
    /// Walks a container's inlines. Inline HTML arrives as flat open/close
    /// tokens rather than as a tree, so an explicit scope stack turns
    /// <c>&lt;b&gt;…&lt;/b&gt;</c> back into nested styling.
    /// </summary>
    private void WriteInlines(ContainerInline? container, InlineWriter writer, RunStyle baseStyle)
    {
        if (container is null) return;

        var scopes = new Stack<HtmlScope>();
        scopes.Push(new HtmlScope("#base", baseStyle, 0, null));

        foreach (var inline in container)
        {
            if (inline is HtmlInline html)
            {
                ApplyHtmlToken(html.Tag, writer, scopes);
                continue;
            }

            WriteInline(inline, writer, scopes.Peek().Style);
        }

        // Close anything the author left open.
        while (scopes.Count > 1) PopScope(writer, scopes);
    }

    private sealed record HtmlScope(string Tag, RunStyle Style, int Start, string? Url);

    private void ApplyHtmlToken(string token, InlineWriter writer, Stack<HtmlScope> scopes)
    {
        var trimmed = token.Trim();
        var isClosing = trimmed.StartsWith("</", StringComparison.Ordinal);

        var nameStart = isClosing ? 2 : 1;
        var nameEnd = nameStart;
        while (nameEnd < trimmed.Length && (char.IsLetterOrDigit(trimmed[nameEnd]) || trimmed[nameEnd] == '-'))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart) return;
        var tag = trimmed[nameStart..nameEnd].ToLowerInvariant();

        if (isClosing)
        {
            if (scopes.Count > 1) PopScope(writer, scopes);
            return;
        }

        switch (tag)
        {
            case "br":
                writer.LineBreak();
                return;
            case "wbr":
                return;
            case "img":
            {
                var alt = HtmlParser.Parse(trimmed).OfType<HtmlElement>().FirstOrDefault()?.Attribute("alt");
                (scopes.Peek().Style with { Foreground = Theme.Muted })
                    .ApplyTo(writer.Add($"🖼 {(string.IsNullOrWhiteSpace(alt) ? "image" : alt)}"));
                return;
            }
        }

        if (HtmlSpec.Stripped.Contains(tag))
        {
            (scopes.Peek().Style with { Foreground = Theme.Muted, Italic = true })
                .ApplyTo(writer.Add($"[<{tag}> removed by GitHub]"));
            return;
        }

        var current = scopes.Peek().Style;
        string? url = null;

        var style = HtmlRenderer.StyleFor(tag, current, Theme) ?? current;

        if (tag == "a")
        {
            url = HtmlParser.Parse(trimmed).OfType<HtmlElement>().FirstOrDefault()?.Attribute("href");
            style = style with { Foreground = Theme.Link };
        }

        scopes.Push(new HtmlScope(tag, style, writer.Offset, url));
    }

    private static void PopScope(InlineWriter writer, Stack<HtmlScope> scopes)
    {
        var scope = scopes.Pop();
        if (scope.Url is { } url) writer.MarkLink(scope.Start, url);
    }

    private void WriteInline(Inline inline, InlineWriter writer, RunStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                style.ApplyTo(writer.Add(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
            {
                var nested = emphasis.DelimiterChar switch
                {
                    '~' => style with { Strikethrough = true },
                    _ when emphasis.DelimiterCount >= 2 => style with { Bold = true },
                    _ => style with { Italic = true },
                };
                WriteInlines(emphasis, writer, nested);
                break;
            }

            case CodeInline code:
                (style with
                {
                    Font = Theme.MonoFont,
                    Background = Theme.CodeBackground,
                    Foreground = Theme.CodeText,
                    FontSize = Theme.FontSize - 1,
                }).ApplyTo(writer.Add(code.Content));
                break;

            case MathInline math:
                (style with
                {
                    Font = Theme.MonoFont,
                    Background = Theme.SubtleBackground,
                    Foreground = Theme.Text,
                    FontSize = Theme.FontSize - 1,
                }).ApplyTo(writer.Add(math.Content.ToString()));
                break;

            case TaskList:
                // Drawn in the list marker gutter by RenderList, not inline.
                break;

            case LinkInline { IsImage: true } image:
            {
                var alt = InlineText(image);
                (style with { Foreground = Theme.Muted })
                    .ApplyTo(writer.Add($"🖼 {(string.IsNullOrWhiteSpace(alt) ? "image" : alt)}"));
                break;
            }

            case LinkInline link:
            {
                var offset = writer.Offset;
                WriteInlines(link, writer, style with { Foreground = Theme.Link });
                writer.MarkLink(offset, link.Url ?? string.Empty);
                break;
            }

            case AutolinkInline autolink:
            {
                var offset = writer.Offset;
                var url = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
                (style with { Foreground = Theme.Link }).ApplyTo(writer.Add(autolink.Url));
                writer.MarkLink(offset, url);
                break;
            }

            case FootnoteLink footnote:
            {
                var label = footnote.IsBackLink ? " ↩" : $"[{footnote.Index}]";
                (style with { Foreground = Theme.Link, FontSize = Theme.FontSize - 3 })
                    .ApplyTo(writer.Add(label));
                break;
            }

            case HtmlEntityInline entity:
                style.ApplyTo(writer.Add(entity.Transcoded.ToString()));
                break;

            case LineBreakInline lineBreak:
                if (lineBreak.IsHard) writer.LineBreak();
                else style.ApplyTo(writer.Add(" "));
                break;

            case ContainerInline container:
                WriteInlines(container, writer, style);
                break;
        }
    }

    private static string InlineText(ContainerInline container)
    {
        var builder = new StringBuilder();
        foreach (var inline in container.Descendants<LiteralInline>())
        {
            builder.Append(inline.Content.ToString());
        }
        return builder.ToString();
    }

    /// <summary>Raw source text of a leaf block, lines rejoined.</summary>
    private static string LinesOf(LeafBlock block)
    {
        var builder = new StringBuilder();
        var lines = block.Lines.Lines;
        if (lines is null) return string.Empty;

        for (var i = 0; i < block.Lines.Count; i++)
        {
            builder.Append(lines[i].Slice.ToString());
            builder.Append('\n');
        }
        return builder.ToString();
    }
}
