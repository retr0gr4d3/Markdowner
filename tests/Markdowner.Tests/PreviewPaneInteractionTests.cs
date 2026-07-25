using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdowner.Models;
using Markdowner.Rendering;
using Markdowner.ViewModels;
using Markdowner.Views;
using Xunit;

namespace Markdowner.Tests;

/// <summary>
/// Exercises the spoiler through the real window — real view model, real preview
/// pane, real scroll viewer — rather than a renderer output hosted on its own.
/// A synthetic host proved the renderer worked while the shipping app did not.
/// </summary>
public class PreviewPaneInteractionTests
{
    private static (MainWindow Window, MainWindowViewModel Model) OpenWith(
        string markdown, MarkdownFlavor flavor)
    {
        var model = new MainWindowViewModel { Flavor = flavor };
        model.Document.Text = markdown;

        var window = new MainWindow { DataContext = model };
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        return (window, model);
    }

    private static MarkdownTextBlock SpoilerBlock(MainWindow window) =>
        window.GetVisualDescendants()
            .OfType<MarkdownTextBlock>()
            .First(block => block.Spoilers.Count > 0);

    private static void ClickCharacter(MainWindow window, MarkdownTextBlock block, int characterIndex)
    {
        var glyph = block.TextLayout.HitTestTextPosition(characterIndex);

        var local = new Point(
            glyph.X + glyph.Width / 2 + block.Padding.Left,
            glyph.Y + glyph.Height / 2 + block.Padding.Top);

        var point = block.TranslatePoint(local, window)
                    ?? throw new InvalidOperationException("the spoiler is not in the visual tree");

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static byte[] Pixels(MainWindow window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();

        var bytes = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);
        return bytes;
    }

    [AvaloniaFact]
    public void PreviewPane_RendersTheSpoiler()
    {
        var (window, _) = OpenWith("||secret||", MarkdownFlavor.Discord);

        var block = SpoilerBlock(window);
        Assert.Single(block.Spoilers);
        Assert.False(block.Spoilers[0].IsRevealed);
    }

    [AvaloniaFact]
    public void PreviewPane_SpoilerRevealsWhenClicked()
    {
        var (window, _) = OpenWith("||secret||", MarkdownFlavor.Discord);
        var block = SpoilerBlock(window);

        ClickCharacter(window, block, 2);

        Assert.True(block.Spoilers[0].IsRevealed);
    }

    [AvaloniaFact]
    public void PreviewPane_SpoilerBecomesVisibleWhenClicked()
    {
        var (window, _) = OpenWith("||secret||", MarkdownFlavor.Discord);
        var block = SpoilerBlock(window);

        var before = Pixels(window);
        ClickCharacter(window, block, 2);
        var after = Pixels(window);

        Assert.False(before.SequenceEqual(after),
            "clicking the spoiler in the preview pane changed nothing on screen");
    }

    [AvaloniaFact]
    public void StarterDocument_SpoilerRevealsAfterSwitchingToDiscord()
    {
        // The exact reported path: open the app, pick Discord from the toolbar,
        // click the spoiler. In the starter document that spoiler sits on the
        // second line of its paragraph, which is what broke hit testing.
        var (window, model) = OpenWith(SampleDocuments.Default, MarkdownFlavor.GitHub);

        model.FlavorIndex = (int)MarkdownFlavor.Discord;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var block = SpoilerBlock(window);
        block.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var spoiler = block.Spoilers[0];
        Assert.False(spoiler.IsRevealed);

        ClickCharacter(window, block, SpoilerCharacterIndex(block));

        Assert.True(spoiler.IsRevealed);
    }

    /// <summary>A character index that falls inside the block's first spoiler.</summary>
    private static int SpoilerCharacterIndex(MarkdownTextBlock block)
    {
        var text = block.Inlines?.Text ?? string.Empty;

        for (var i = 0; i < text.Length; i++)
        {
            if (block.Spoilers[0].Contains(i) && !char.IsWhiteSpace(text[i])) return i;
        }

        throw new InvalidOperationException("no spoiler character found");
    }

    [AvaloniaFact]
    public void PreviewPane_SpoilerStillWorksAfterSwitchingRenderer()
    {
        // Start on GitHub (where the pipes show literally), then switch.
        var (window, model) = OpenWith("||secret||", MarkdownFlavor.GitHub);

        model.Flavor = MarkdownFlavor.Discord;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var block = SpoilerBlock(window);
        ClickCharacter(window, block, 2);

        Assert.True(block.Spoilers[0].IsRevealed);
    }
}
