using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Markdowner.Html;

namespace Markdowner.Rendering;

/// <summary>
/// Renders the HTML that GitHub allows inside Markdown. Tags outside the
/// sanitizer allow-list are shown as an explicit "removed by GitHub" marker
/// rather than silently rendered — the preview should tell you when what you
/// wrote will not survive the round trip.
/// </summary>
public sealed partial class HtmlRenderer(PreviewTheme theme)
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRe { get; }

    private int _listLevel;

    public IEnumerable<Control> Render(string html) => RenderNodes(HtmlParser.Parse(html));

    public IEnumerable<Control> RenderNodes(IReadOnlyList<HtmlNode> nodes)
    {
        var inlineRun = new List<HtmlNode>();

        foreach (var node in nodes)
        {
            if (IsBlockLevel(node))
            {
                if (TryFlush(inlineRun) is { } paragraph) yield return paragraph;
                foreach (var control in RenderBlock((HtmlElement)node)) yield return control;
            }
            else
            {
                inlineRun.Add(node);
            }
        }

        if (TryFlush(inlineRun) is { } tail) yield return tail;
    }

    private static bool IsBlockLevel(HtmlNode node) =>
        node is HtmlElement element &&
        (HtmlSpec.IsBlock(element.Tag) || HtmlSpec.Stripped.Contains(element.Tag));

    /// <summary>Turns buffered inline nodes into a paragraph, or nothing if they were only whitespace.</summary>
    private Control? TryFlush(List<HtmlNode> buffer)
    {
        if (buffer.Count == 0) return null;

        var meaningful = buffer.Any(n => n is not HtmlText text || text.Text.Trim().Length > 0);
        var nodes = buffer.ToList();
        buffer.Clear();

        if (!meaningful) return null;

        var writer = new InlineWriter(theme);
        WriteInlines(nodes, writer, default);
        return writer.IsEmpty ? null : writer.Build();
    }

    // ------------------------------------------------------------- blocks

    private IEnumerable<Control> RenderBlock(HtmlElement element)
    {
        if (HtmlSpec.Stripped.Contains(element.Tag))
        {
            yield return BlockFactory.Placeholder(theme, "⚠",
                $"<{element.Tag}> is removed by GitHub's HTML sanitizer.");
            yield break;
        }

        switch (element.Tag.ToLowerInvariant())
        {
            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                yield return Heading(element, element.Tag[1] - '0');
                break;

            case "hr":
                yield return BlockFactory.Rule(theme);
                break;

            case "br":
                yield return new Border { Height = 6 };
                break;

            case "blockquote":
                yield return BlockFactory.Quote(theme, RenderNodes(element.Children).ToList());
                break;

            case "pre":
                yield return PreformattedBlock(element);
                break;

            case "ul" or "ol":
                foreach (var control in RenderList(element)) yield return control;
                break;

            case "dl":
                foreach (var control in RenderDefinitionList(element)) yield return control;
                break;

            case "table":
                yield return RenderTable(element);
                break;

            case "details":
                yield return RenderDetails(element);
                break;

            case "img":
                yield return ImagePlaceholder(element);
                break;

            case "video":
                yield return BlockFactory.Placeholder(theme, "▶",
                    "Video attachment", element.Attribute("src"));
                break;

            case "picture":
                yield return RenderPicture(element);
                break;

            case "aside":
                yield return new Border
                {
                    Padding = new Thickness(14, 0, 0, 0),
                    Child = Wrap(RenderNodes(element.Children)),
                };
                break;

            // Generic block containers just pass their children through.
            default:
                foreach (var control in RenderNodes(element.Children)) yield return control;
                break;
        }
    }

    private Control Heading(HtmlElement element, int level)
    {
        var writer = new InlineWriter(theme);
        WriteInlines(element.Children, writer, new RunStyle { Bold = true, Foreground = theme.Heading });

        var size = HeadingSize(level);
        var block = writer.Build(fontSize: size, foreground: theme.Heading,
            fontWeight: FontWeight.Bold, lineHeight: size + 8);
        block.Margin = new Thickness(0, level <= 2 ? 12 : 8, 0, 0);

        if (level > 2) return block;

        var stack = BlockFactory.Stack(6);
        stack.Children.Add(block);
        stack.Children.Add(new Border { Height = 1, Background = theme.Border });
        return stack;
    }

    private double HeadingSize(int level) => level switch
    {
        1 => theme.FontSize + 11,
        2 => theme.FontSize + 7,
        3 => theme.FontSize + 3.5,
        4 => theme.FontSize + 1,
        5 => theme.FontSize - 0.5,
        _ => theme.FontSize - 1.5,
    };

    private Control PreformattedBlock(HtmlElement element)
    {
        // <pre><code class="language-x"> is the conventional shape.
        var code = element.Children.OfType<HtmlElement>()
            .FirstOrDefault(e => e.Tag.Equals("code", StringComparison.OrdinalIgnoreCase));

        var language = string.Empty;
        if (code?.Attribute("class") is { } className)
        {
            var token = className.Split(' ')
                .FirstOrDefault(c => c.StartsWith("language-", StringComparison.OrdinalIgnoreCase));
            if (token is not null) language = token["language-".Length..];
        }

        var source = code is not null ? RawText(code) : RawText(element);
        return BlockFactory.CodeBlock(theme, source.Trim('\n'), language);
    }

    private IEnumerable<Control> RenderList(HtmlElement element)
    {
        var ordered = element.Tag.Equals("ol", StringComparison.OrdinalIgnoreCase);
        var number = 1;
        if (ordered && int.TryParse(element.Attribute("start"), out var start)) number = start;

        foreach (var item in element.Children.OfType<HtmlElement>())
        {
            if (!item.Tag.Equals("li", StringComparison.OrdinalIgnoreCase)) continue;

            var marker = ordered
                ? $"{number++}."
                : _listLevel switch { 0 => "•", 1 => "◦", _ => "▪" };

            var level = _listLevel;
            _listLevel++;
            var content = Wrap(RenderNodes(item.Children));
            _listLevel--;

            yield return BlockFactory.ListRow(theme, marker, content, level);
        }
    }

    private IEnumerable<Control> RenderDefinitionList(HtmlElement element)
    {
        foreach (var child in element.Children.OfType<HtmlElement>())
        {
            if (child.Tag.Equals("dt", StringComparison.OrdinalIgnoreCase))
            {
                var writer = new InlineWriter(theme);
                WriteInlines(child.Children, writer, new RunStyle { Bold = true });
                var block = writer.Build(fontWeight: FontWeight.Bold);
                block.Margin = new Thickness(0, 6, 0, 0);
                yield return block;
            }
            else if (child.Tag.Equals("dd", StringComparison.OrdinalIgnoreCase))
            {
                yield return new Border
                {
                    Padding = new Thickness(22, 0, 0, 0),
                    Child = Wrap(RenderNodes(child.Children)),
                };
            }
        }
    }

    private Control RenderTable(HtmlElement element)
    {
        var rows = new List<IReadOnlyList<TableCellSpec>>();

        foreach (var row in DescendantRows(element))
        {
            var cells = new List<TableCellSpec>();
            foreach (var cell in row.Children.OfType<HtmlElement>())
            {
                var isHeader = cell.Tag.Equals("th", StringComparison.OrdinalIgnoreCase);
                if (!isHeader && !cell.Tag.Equals("td", StringComparison.OrdinalIgnoreCase)) continue;

                var writer = new InlineWriter(theme);
                WriteInlines(cell.Children, writer, isHeader ? new RunStyle { Bold = true } : default);

                cells.Add(new TableCellSpec(
                    writer.Build(fontWeight: isHeader ? FontWeight.Bold : null),
                    isHeader,
                    ParseSpan(cell.Attribute("colspan")),
                    ParseSpan(cell.Attribute("rowspan"))));
            }

            if (cells.Count > 0) rows.Add(cells);
        }

        return TableFactory.Build(theme, rows);
    }

    private static int ParseSpan(string? value) =>
        int.TryParse(value, out var span) && span is > 0 and <= 100 ? span : 1;

    /// <summary>Collects &lt;tr&gt; whether or not the author used a row group.</summary>
    private static IEnumerable<HtmlElement> DescendantRows(HtmlElement table)
    {
        foreach (var child in table.Children.OfType<HtmlElement>())
        {
            if (child.Tag.Equals("tr", StringComparison.OrdinalIgnoreCase))
            {
                yield return child;
            }
            else if (child.Tag.Equals("thead", StringComparison.OrdinalIgnoreCase) ||
                     child.Tag.Equals("tbody", StringComparison.OrdinalIgnoreCase) ||
                     child.Tag.Equals("tfoot", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var row in child.Children.OfType<HtmlElement>())
                {
                    if (row.Tag.Equals("tr", StringComparison.OrdinalIgnoreCase)) yield return row;
                }
            }
        }
    }

    private Control RenderDetails(HtmlElement element)
    {
        var summary = element.Children.OfType<HtmlElement>()
            .FirstOrDefault(e => e.Tag.Equals("summary", StringComparison.OrdinalIgnoreCase));

        var headerWriter = new InlineWriter(theme);
        if (summary is not null)
        {
            WriteInlines(summary.Children, headerWriter, default);
        }
        else
        {
            headerWriter.Add("Details");
        }

        var body = element.Children.Where(c => !ReferenceEquals(c, summary)).ToList();

        return new Expander
        {
            Header = headerWriter.Build(),
            Content = Wrap(RenderNodes(body)),
            IsExpanded = element.Attributes.ContainsKey("open"),
            Background = theme.SubtleBackground,
            BorderBrush = theme.Border,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
    }

    private Control RenderPicture(HtmlElement element)
    {
        var img = element.Children.OfType<HtmlElement>()
            .FirstOrDefault(e => e.Tag.Equals("img", StringComparison.OrdinalIgnoreCase));

        return img is not null
            ? ImagePlaceholder(img)
            : BlockFactory.Placeholder(theme, "🖼", "Picture");
    }

    private Control ImagePlaceholder(HtmlElement element)
    {
        var alt = element.Attribute("alt");
        var caption = string.IsNullOrWhiteSpace(alt) ? "Image" : alt;
        return BlockFactory.Placeholder(theme, "🖼", caption, element.Attribute("src"));
    }

    private Control Wrap(IEnumerable<Control> controls)
    {
        var stack = BlockFactory.Stack(6);
        foreach (var control in controls) stack.Children.Add(control);
        return stack;
    }

    // ------------------------------------------------------------ inlines

    /// <summary>
    /// The styling effect of a purely presentational inline tag, or null if the
    /// tag needs structural handling (links, breaks, images, sub/sup, ...).
    /// </summary>
    public static RunStyle? StyleFor(string tag, RunStyle style, PreviewTheme theme) =>
        tag.ToLowerInvariant() switch
        {
            "b" or "strong" => style with { Bold = true },
            "i" or "em" or "cite" or "dfn" or "var" => style with { Italic = true },
            "ins" => style with { Underline = true },
            "s" or "strike" or "del" => style with { Strikethrough = true },
            "mark" => style with { Background = theme.MarkBackground, Foreground = theme.MarkText },
            "small" => style with { FontSize = (style.FontSize ?? theme.FontSize) - 2 },
            "code" or "samp" or "tt" => style with
            {
                Font = theme.MonoFont,
                Background = theme.CodeBackground,
                Foreground = theme.CodeText,
                FontSize = (style.FontSize ?? theme.FontSize) - 1,
            },
            "kbd" => style with
            {
                Font = theme.MonoFont,
                Background = theme.SubtleBackground,
                Foreground = theme.Text,
                FontSize = (style.FontSize ?? theme.FontSize) - 1.5,
            },
            _ => null,
        };

    public void WriteInlines(IReadOnlyList<HtmlNode> nodes, InlineWriter writer, RunStyle style)
    {
        foreach (var node in nodes) WriteInline(node, writer, style);
    }

    private void WriteInline(HtmlNode node, InlineWriter writer, RunStyle style)
    {
        if (node is HtmlText text)
        {
            var normalized = WhitespaceRe.Replace(text.Text, " ");
            if (normalized.Length == 0) return;
            style.ApplyTo(writer.Add(normalized));
            return;
        }

        if (node is not HtmlElement element) return;

        if (HtmlSpec.Stripped.Contains(element.Tag))
        {
            (style with { Foreground = theme.Muted, Italic = true })
                .ApplyTo(writer.Add($"[<{element.Tag}> removed by GitHub]"));
            return;
        }

        // Tags whose only effect is inline styling share one table with the
        // GitHub renderer, which sees the same tags as flat open/close tokens.
        if (StyleFor(element.Tag, style, theme) is { } styled)
        {
            WriteInlines(element.Children, writer, styled);
            return;
        }

        switch (element.Tag.ToLowerInvariant())
        {
            case "sub" or "sup":
                WriteSubSuper(element, writer, style);
                break;

            case "a":
            {
                var offset = writer.Offset;
                WriteInlines(element.Children, writer, style with { Foreground = theme.Link });
                if (element.Attribute("href") is { } href) writer.MarkLink(offset, href);
                break;
            }

            case "br":
                writer.LineBreak();
                break;

            case "wbr":
                break;

            case "q":
                style.ApplyTo(writer.Add("“"));
                WriteInlines(element.Children, writer, style);
                style.ApplyTo(writer.Add("”"));
                break;

            case "abbr":
                WriteInlines(element.Children, writer, style with { Underline = true });
                if (element.Attribute("title") is { } title)
                {
                    (style with { Foreground = theme.Muted }).ApplyTo(writer.Add($" ({title})"));
                }
                break;

            case "rt":
                (style with { Foreground = theme.Muted, FontSize = theme.FontSize - 4 })
                    .ApplyTo(writer.Add("("));
                WriteInlines(element.Children, writer,
                    style with { Foreground = theme.Muted, FontSize = theme.FontSize - 4 });
                (style with { Foreground = theme.Muted, FontSize = theme.FontSize - 4 })
                    .ApplyTo(writer.Add(")"));
                break;

            case "rp":
                break;

            case "img":
            {
                var alt = element.Attribute("alt");
                (style with { Foreground = theme.Muted }).ApplyTo(
                    writer.Add($"🖼 {(string.IsNullOrWhiteSpace(alt) ? "image" : alt)}"));
                break;
            }

            default:
                WriteInlines(element.Children, writer, style);
                break;
        }
    }

    private void WriteSubSuper(HtmlElement element, InlineWriter writer, RunStyle style)
    {
        var isSuper = element.Tag.Equals("sup", StringComparison.OrdinalIgnoreCase);
        var start = writer.InlineCount;

        WriteInlines(element.Children, writer,
            style with { FontSize = (style.FontSize ?? theme.FontSize) * 0.75 });

        // Avalonia exposes baseline shifting on inlines, which beats faking it with size alone.
        for (var i = start; i < writer.InlineCount; i++)
        {
            writer.SetBaseline(i, isSuper ? BaselineAlignment.Superscript : BaselineAlignment.Subscript);
        }
    }

    /// <summary>Text content with whitespace preserved, for &lt;pre&gt; and &lt;code&gt;.</summary>
    private static string RawText(HtmlNode node)
    {
        var builder = new StringBuilder();
        Collect(node, builder);
        return builder.ToString();

        static void Collect(HtmlNode current, StringBuilder builder)
        {
            switch (current)
            {
                case HtmlText text:
                    builder.Append(text.Text);
                    break;
                case HtmlElement element:
                    if (element.Tag.Equals("br", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append('\n');
                        return;
                    }
                    foreach (var child in element.Children) Collect(child, builder);
                    break;
            }
        }
    }
}
