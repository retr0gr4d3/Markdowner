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

    public MarkdownTextBlock()
    {
        // Avalonia skips controls with a null Background during hit testing, and
        // style selectors match the exact type, so this subclass never picks up
        // the theme's SelectableTextBlock background. Without this, pointer
        // events land on the parent panel and links and spoilers are dead.
        Background = Brushes.Transparent;
    }

    /// <summary>How far the pointer may travel between press and release and still count as a click.</summary>
    private const double ClickSlop = 4;

    /// <summary>Rounding tolerance when confirming a pointer landed on a glyph.</summary>
    private const double HitSlop = 1;

    private Point? _pressedAt;

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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var pressedAt = _pressedAt;
        _pressedAt = null;

        base.OnPointerReleased(e);

        if (e.InitialPressMouseButton != MouseButton.Left) return;

        var releasedAt = e.GetPosition(this);

        // A click is a press and release in roughly the same place. Comparing
        // positions is far more reliable than inspecting the selection, which a
        // one-pixel drift is enough to populate.
        if (pressedAt is null) return;
        if (Distance(pressedAt.Value, releasedAt) > ClickSlop) return;

        if (HitTest(releasedAt) is not { } position) return;

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

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Maps a pointer position to a character index, or null if the point isn't
    /// over a glyph.
    /// <para>
    /// Deliberately does not trust <c>TextHitTestResult.IsInside</c>: Avalonia
    /// reports it as false for every line after the first, which silently killed
    /// clicks on any spoiler or link below line one. The returned
    /// <c>TextPosition</c> is correct, so the hit is confirmed against that
    /// character's own glyph rectangle instead.
    /// </para>
    /// </summary>
    private int? HitTest(Point point)
    {
        var origin = point - new Point(Padding.Left, Padding.Top);
        if (origin.X < 0 || origin.Y < 0) return null;

        var layout = TextLayout;
        if (origin.Y > layout.Height) return null;

        var position = layout.HitTestPoint(origin).TextPosition;
        var glyph = layout.HitTestTextPosition(position);

        // Rejects clicks in the empty space past the end of a line, where the
        // hit test still reports the nearest character.
        if (origin.X < glyph.X - HitSlop || origin.X > glyph.Right + HitSlop) return null;
        if (origin.Y < glyph.Y - HitSlop || origin.Y > glyph.Bottom + HitSlop) return null;

        return position;
    }
}
