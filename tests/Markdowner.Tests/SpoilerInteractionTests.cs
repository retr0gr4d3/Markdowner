using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Markdowner.Rendering;
using Markdowner.Tests;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Markdowner.Tests;

public static class TestAppBuilder
{
    // The real App is used so tests load exactly the styles the app ships with.
    // Real text layout (Skia + a bundled font) is required too: the click-to-reveal
    // path resolves the spoiler by hit-testing the laid-out text.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Markdowner.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
/// Drives the preview the way a user does — a real pointer press and release on
/// a laid-out control — because spoilers and links are resolved by hit-testing
/// text, which unit tests over the control tree alone cannot exercise.
/// </summary>
public class SpoilerInteractionTests
{
    private static (Window Window, MarkdownTextBlock Block) Show(string markdown)
    {
        var content = new DiscordRenderer().Render(markdown);

        var window = new Window
        {
            Width = 500,
            Height = 300,
            Content = content,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var block = Descendants(content).OfType<MarkdownTextBlock>().First();
        return (window, block);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;

        var children = root switch
        {
            Panel panel => panel.Children.AsEnumerable(),
            Decorator decorator => decorator.Child is { } c ? [c] : Array.Empty<Control>(),
            ContentControl content => content.Content is Control c ? [c] : Array.Empty<Control>(),
            _ => [],
        };

        foreach (var child in children)
        {
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    /// <summary>
    /// Clicks the glyph at a character index. Targeting the control's centre
    /// would miss: the block stretches to the pane width while the text stops
    /// wherever it stops, and clicks past the last glyph are correctly ignored.
    /// </summary>
    private static void ClickCharacter(Window window, MarkdownTextBlock block, int characterIndex)
    {
        var glyph = block.TextLayout.HitTestTextPosition(characterIndex);

        var local = new Point(
            glyph.X + glyph.Width / 2 + block.Padding.Left,
            glyph.Y + glyph.Height / 2 + block.Padding.Top);

        var point = block.TranslatePoint(local, window) ?? local;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    /// <summary>Raw pixels of the rendered window, for "did anything actually change" checks.</summary>
    private static byte[] Pixels(Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    [AvaloniaFact]
    public void Spoiler_BecomesVisibleWhenClicked()
    {
        // Weak on its own: CaptureRenderedFrame forces a repaint, so this passes
        // even when nothing invalidated the layout. See the TextLayout test below.
        var (window, block) = Show("||secret||");

        var before = Pixels(window);
        ClickCharacter(window, block, 2);
        var after = Pixels(window);

        Assert.False(before.SequenceEqual(after), "revealing the spoiler did not change what is drawn");
    }

    [AvaloniaFact]
    public void RevealingASpoiler_InvalidatesTheTextLayout()
    {
        // Brushes are baked into the text layout when it is built, so changing a
        // run's foreground is invisible until the layout is rebuilt. Without this
        // the spoiler stays black on screen until something else forces a repaint.
        var (window, block) = Show("||secret||");
        var layoutBefore = block.TextLayout;

        ClickCharacter(window, block, 2);

        Assert.NotSame(layoutBefore, block.TextLayout);
    }

    [AvaloniaFact]
    public void Spoiler_RevealsEvenWhenThePointerDriftsSlightly()
    {
        // A real mouse or trackpad almost never releases on the exact pixel it
        // pressed. That drift starts a text selection, which must not be
        // mistaken for a deliberate drag-select.
        var (window, block) = Show("||secret||");
        var spoiler = Assert.Single(block.Spoilers);

        var glyph = block.TextLayout.HitTestTextPosition(2);
        var start = block.TranslatePoint(
            new Point(glyph.X + glyph.Width / 2, glyph.Y + glyph.Height / 2), window)!.Value;
        var drifted = start + new Vector(3, 1);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(drifted);
        window.MouseUp(drifted, MouseButton.Left);

        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void DraggingAcrossTheSpoiler_SelectsInsteadOfRevealing()
    {
        // A deliberate drag is a selection gesture, not a click.
        var (window, block) = Show("||a long spoiler worth selecting||");
        var spoiler = Assert.Single(block.Spoilers);

        var from = block.TextLayout.HitTestTextPosition(1);
        var to = block.TextLayout.HitTestTextPosition(15);

        var start = block.TranslatePoint(new Point(from.X, from.Y + from.Height / 2), window)!.Value;
        var end = block.TranslatePoint(new Point(to.X, to.Y + to.Height / 2), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end);
        window.MouseUp(end, MouseButton.Left);

        Assert.False(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void Spoiler_AfterAHardLineBreak_RevealsWhenClicked()
    {
        // Discord treats a single newline as a hard break, so a spoiler often
        // sits on a later line of the same paragraph. Character offsets recorded
        // for the spoiler must line up with the laid-out text across that break.
        var (window, block) = Show("first line\n||secret||");
        var spoiler = Assert.Single(block.Spoilers);

        ClickCharacter(window, block, 13);

        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void Spoiler_AfterSeveralLineBreaks_RevealsWhenClicked()
    {
        var (window, block) = Show("one\ntwo\nthree\n||secret||");
        var spoiler = Assert.Single(block.Spoilers);

        ClickCharacter(window, block, 16);

        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void Spoiler_StartsHidden()
    {
        var (_, block) = Show("||secret||");

        var spoiler = Assert.Single(block.Spoilers);
        Assert.False(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void Spoiler_RevealsWhenClicked()
    {
        var (window, block) = Show("||secret||");
        var spoiler = Assert.Single(block.Spoilers);

        ClickCharacter(window, block, 2);

        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void TheSpoilerIsHitTestable()
    {
        // Regression: a null Background made the block invisible to hit testing,
        // so every click fell through to the panel behind it.
        var (window, block) = Show("||secret||");
        window.CaptureRenderedFrame();

        var glyph = block.TextLayout.HitTestTextPosition(2);
        var point = block.TranslatePoint(new Point(glyph.X + glyph.Width / 2, glyph.Y + glyph.Height / 2), window);

        Assert.IsType<MarkdownTextBlock>(window.InputHitTest(point!.Value));
    }

    [AvaloniaFact]
    public void ClickOutsideTheSpoiler_LeavesItHidden()
    {
        // Rendered text is "a and a long tail..." — the spoiler is just "a".
        var (window, block) = Show("||a|| and a long tail of ordinary text here");
        var spoiler = Assert.Single(block.Spoilers);

        ClickCharacter(window, block, 12);

        Assert.False(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void RevealedSpoiler_StaysRevealed()
    {
        var (window, block) = Show("||secret||");
        var spoiler = Assert.Single(block.Spoilers);

        ClickCharacter(window, block, 2);
        ClickCharacter(window, block, 2);

        Assert.True(spoiler.IsRevealed);
    }

    [AvaloniaFact]
    public void GitHubLinks_AreAlsoHitTestable()
    {
        // Links resolve through the same hit-test path as spoilers, so the same
        // null-Background bug made them unclickable too. (The click itself isn't
        // simulated here - that would launch a browser.)
        var content = new GitHubRenderer().Render("[label](https://example.com)");
        var window = new Window { Width = 500, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var block = Descendants(content).OfType<MarkdownTextBlock>().First();
        Assert.Single(block.Links);

        var glyph = block.TextLayout.HitTestTextPosition(2);
        var point = block.TranslatePoint(new Point(glyph.X + glyph.Width / 2, glyph.Y + glyph.Height / 2), window);

        Assert.IsType<MarkdownTextBlock>(window.InputHitTest(point!.Value));
    }

    [AvaloniaFact]
    public void EachSpoilerRevealsIndependently()
    {
        var (window, block) = Show("||one|| plain ||two||");
        Assert.Equal(2, block.Spoilers.Count);

        ClickCharacter(window, block, 1);

        Assert.True(block.Spoilers[0].IsRevealed);
        Assert.False(block.Spoilers[1].IsRevealed);
    }
}
