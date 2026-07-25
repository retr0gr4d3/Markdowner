using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace Markdowner.Rendering;

public sealed record LinkSpan(int Start, int Length, string Url)
{
    public bool Contains(int position) => position >= Start && position < Start + Length;
}

/// <summary>A Discord spoiler: the runs are painted in their own background colour until clicked.</summary>
public sealed class SpoilerSpan(int start, int length, IReadOnlyList<Run> runs, IBrush revealedForeground)
{
    public bool Contains(int position) => position >= start && position < start + length;

    public bool IsRevealed { get; private set; }

    public void Reveal()
    {
        if (IsRevealed) return;
        IsRevealed = true;
        foreach (var run in runs) run.Foreground = revealedForeground;
    }
}

/// <summary>
/// Selectable text that also knows which character ranges are links and which
/// are spoilers, so both can stay ordinary inline runs and wrap normally
/// instead of being boxed into <see cref="InlineUIContainer"/>s.
/// </summary>
public sealed class MarkdownTextBlock : SelectableTextBlock
{
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor IBeamCursor = new(StandardCursorType.Ibeam);

    public IReadOnlyList<LinkSpan> Links { get; init; } = [];
    public IReadOnlyList<SpoilerSpan> Spoilers { get; init; } = [];

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (Links.Count == 0 && Spoilers.Count == 0) return;

        var position = HitTest(e.GetPosition(this));
        var interactive = position is { } p &&
                          (LinkAt(p) is not null || SpoilerAt(p) is { IsRevealed: false });

        Cursor = interactive ? HandCursor : IBeamCursor;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        // Only treat this as a click when the user wasn't dragging out a selection.
        var wasSelecting = SelectionStart != SelectionEnd;

        base.OnPointerReleased(e);

        if (wasSelecting || e.InitialPressMouseButton != MouseButton.Left) return;
        if (HitTest(e.GetPosition(this)) is not { } position) return;

        if (SpoilerAt(position) is { IsRevealed: false } spoiler)
        {
            spoiler.Reveal();
            e.Handled = true;
            return;
        }

        if (LinkAt(position) is { } link)
        {
            LinkLauncher.Open(this, link.Url);
            e.Handled = true;
        }
    }

    private LinkSpan? LinkAt(int position)
    {
        foreach (var link in Links)
        {
            if (link.Contains(position)) return link;
        }
        return null;
    }

    private SpoilerSpan? SpoilerAt(int position)
    {
        foreach (var spoiler in Spoilers)
        {
            if (spoiler.Contains(position)) return spoiler;
        }
        return null;
    }

    private int? HitTest(Point point)
    {
        var textOrigin = point - new Point(Padding.Left, Padding.Top);
        if (textOrigin.X < 0 || textOrigin.Y < 0) return null;

        var hit = TextLayout.HitTestPoint(textOrigin);
        return hit.IsInside ? hit.TextPosition : null;
    }
}
