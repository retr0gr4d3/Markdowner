using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Markdowner.Rendering;

/// <summary>
/// Accumulates inline runs while tracking the plain-text offset of everything
/// written, so link and spoiler ranges can be recorded as character spans and
/// resolved later by hit-testing the finished text layout.
/// </summary>
public sealed class InlineWriter(PreviewTheme theme)
{
    private readonly List<Inline> _inlines = [];
    private readonly List<LinkSpan> _links = [];
    private readonly List<SpoilerSpan> _spoilers = [];
    private int _offset;

    public PreviewTheme Theme { get; } = theme;

    /// <summary>Current plain-text offset — capture before writing a span, pass to Mark* after.</summary>
    public int Offset => _offset;

    public int InlineCount => _inlines.Count;

    public bool IsEmpty => _inlines.Count == 0;

    public Run Add(string text)
    {
        var run = new Run(text);
        _inlines.Add(run);
        _offset += text.Length;
        return run;
    }

    public void LineBreak()
    {
        _inlines.Add(new LineBreak());
        _offset += 1;
    }

    /// <summary>Shifts an already-written inline off the baseline, for &lt;sub&gt;/&lt;sup&gt;.</summary>
    public void SetBaseline(int inlineIndex, BaselineAlignment alignment)
    {
        if (inlineIndex >= 0 && inlineIndex < _inlines.Count)
        {
            _inlines[inlineIndex].BaselineAlignment = alignment;
        }
    }

    public void MarkLink(int start, string url)
    {
        if (_offset > start && !string.IsNullOrWhiteSpace(url))
        {
            _links.Add(new LinkSpan(start, _offset - start, url));
        }
    }

    /// <summary>Marks everything written since <paramref name="startOffset"/> as one spoiler.</summary>
    public void MarkSpoiler(int startOffset, int startInlineIndex)
    {
        if (_offset <= startOffset) return;

        var runs = new List<Run>();
        for (var i = startInlineIndex; i < _inlines.Count; i++)
        {
            if (_inlines[i] is Run run) runs.Add(run);
        }

        foreach (var run in runs)
        {
            run.Background = Theme.SpoilerHidden;
            run.Foreground = Theme.SpoilerHidden;
        }

        _spoilers.Add(new SpoilerSpan(startOffset, _offset - startOffset, runs, Theme.SpoilerRevealed));
    }

    public MarkdownTextBlock Build(double? fontSize = null, IBrush? foreground = null,
        FontWeight? fontWeight = null, double? lineHeight = null)
    {
        var block = new MarkdownTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontFamily = Theme.BodyFont,
            FontSize = fontSize ?? Theme.FontSize,
            Foreground = foreground ?? Theme.Text,
            LineHeight = lineHeight ?? Theme.LineHeight,
            Links = _links,
            Spoilers = _spoilers,
        };

        if (fontWeight is { } weight) block.FontWeight = weight;

        foreach (var inline in _inlines) block.Inlines!.Add(inline);
        return block;
    }
}
