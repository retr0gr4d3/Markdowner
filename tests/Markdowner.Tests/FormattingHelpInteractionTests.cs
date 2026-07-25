using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdowner.Models;
using Markdowner.Rendering;
using Markdowner.Views;
using Xunit;

namespace Markdowner.Tests;

/// <summary>
/// The help window renders each example through the live renderer into its own
/// card, which is a different composition from the main preview pane — so the
/// interactive bits need covering here too.
/// </summary>
public class FormattingHelpInteractionTests
{
    private static FormattingHelpWindow Open(MarkdownFlavor flavor)
    {
        var window = new FormattingHelpWindow();
        window.ShowFlavor(flavor);
        window.Show();

        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        return window;
    }

    [AvaloniaFact]
    public void DiscordHelp_ShowsASpoilerExample()
    {
        var window = Open(MarkdownFlavor.Discord);

        var blocks = window.GetVisualDescendants()
            .OfType<MarkdownTextBlock>()
            .Where(block => block.Spoilers.Count > 0)
            .ToList();

        Assert.NotEmpty(blocks);
        Assert.All(blocks, block => Assert.False(block.Spoilers[0].IsRevealed));
    }

    [AvaloniaFact]
    public void DiscordHelp_SpoilerExampleRevealsWhenClicked()
    {
        var window = Open(MarkdownFlavor.Discord);

        var block = window.GetVisualDescendants()
            .OfType<MarkdownTextBlock>()
            .First(b => b.Spoilers.Count > 0);

        // The spoiler card sits well below the fold; a user would scroll to it.
        block.BringIntoView();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var glyph = block.TextLayout.HitTestTextPosition(2);
        var point = block.TranslatePoint(
            new Point(glyph.X + glyph.Width / 2 + block.Padding.Left,
                      glyph.Y + glyph.Height / 2 + block.Padding.Top),
            window);

        Assert.NotNull(point);

        window.MouseDown(point!.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);

        Assert.True(block.Spoilers[0].IsRevealed);
    }

    [AvaloniaFact]
    public void GitHubHelp_HasNoSpoilers()
    {
        // GitHub has no spoiler syntax, so the reference must not imply one.
        var window = Open(MarkdownFlavor.GitHub);

        Assert.All(
            window.GetVisualDescendants().OfType<MarkdownTextBlock>(),
            block => Assert.Empty(block.Spoilers));
    }
}
