using Avalonia.Media;

namespace Markdowner.Rendering;

/// <summary>
/// Colour and type tokens for a preview skin. The preview deliberately mimics
/// the target platform's own dark theme so what you see reads as GitHub or as
/// Discord, not as "some markdown viewer".
/// </summary>
public sealed class PreviewTheme
{
    public required IBrush Background { get; init; }
    public required IBrush Text { get; init; }
    public required IBrush Muted { get; init; }
    public required IBrush Heading { get; init; }
    public required IBrush Link { get; init; }
    public required IBrush Border { get; init; }
    public required IBrush SubtleBackground { get; init; }
    public required IBrush CodeBackground { get; init; }
    public required IBrush CodeText { get; init; }
    public required IBrush CodeBorder { get; init; }
    public required IBrush QuoteBar { get; init; }
    public required IBrush QuoteText { get; init; }
    public required IBrush MentionBackground { get; init; }
    public required IBrush MentionText { get; init; }
    public required IBrush SpoilerHidden { get; init; }
    public required IBrush SpoilerRevealed { get; init; }
    public required IBrush TableHeaderBackground { get; init; }
    public required IBrush MarkBackground { get; init; }
    public required IBrush MarkText { get; init; }

    public required FontFamily BodyFont { get; init; }
    public required FontFamily MonoFont { get; init; }
    public required double FontSize { get; init; }
    public required double LineHeight { get; init; }

    /// <summary>Padding around the whole document surface.</summary>
    public required Avalonia.Thickness Padding { get; init; }

    private static readonly FontFamily Mono =
        new("Cascadia Mono,SF Mono,Menlo,Consolas,DejaVu Sans Mono,monospace");

    public static PreviewTheme GitHub { get; } = new()
    {
        Background = Brush.Parse("#0D1117"),
        Text = Brush.Parse("#E6EDF3"),
        Muted = Brush.Parse("#9198A1"),
        Heading = Brush.Parse("#F0F6FC"),
        Link = Brush.Parse("#4493F8"),
        Border = Brush.Parse("#3D444D"),
        SubtleBackground = Brush.Parse("#151B23"),
        CodeBackground = Brush.Parse("#262C36"),
        CodeText = Brush.Parse("#E6EDF3"),
        CodeBorder = Brush.Parse("#3D444D"),
        QuoteBar = Brush.Parse("#3D444D"),
        QuoteText = Brush.Parse("#9198A1"),
        MentionBackground = Brush.Parse("#193256"),
        MentionText = Brush.Parse("#78BBFF"),
        SpoilerHidden = Brush.Parse("#262C36"),
        SpoilerRevealed = Brush.Parse("#E6EDF3"),
        TableHeaderBackground = Brush.Parse("#151B23"),
        MarkBackground = Brush.Parse("#5A4B14"),
        MarkText = Brush.Parse("#F8E3A1"),
        BodyFont = FontFamily.Default,
        MonoFont = Mono,
        FontSize = 14.5,
        LineHeight = 22,
        Padding = new Avalonia.Thickness(28, 22),
    };

    public static PreviewTheme Discord { get; } = new()
    {
        Background = Brush.Parse("#313338"),
        Text = Brush.Parse("#DBDEE1"),
        Muted = Brush.Parse("#949BA4"),
        Heading = Brush.Parse("#F2F3F5"),
        Link = Brush.Parse("#00A8FC"),
        Border = Brush.Parse("#3F4147"),
        SubtleBackground = Brush.Parse("#2B2D31"),
        CodeBackground = Brush.Parse("#2B2D31"),
        CodeText = Brush.Parse("#DBDEE1"),
        CodeBorder = Brush.Parse("#1E1F22"),
        QuoteBar = Brush.Parse("#4E5058"),
        QuoteText = Brush.Parse("#DBDEE1"),
        MentionBackground = Brush.Parse("#3C4270"),
        MentionText = Brush.Parse("#C9CDFB"),
        SpoilerHidden = Brush.Parse("#1E1F22"),
        SpoilerRevealed = Brush.Parse("#DBDEE1"),
        TableHeaderBackground = Brush.Parse("#2B2D31"),
        MarkBackground = Brush.Parse("#5A4B14"),
        MarkText = Brush.Parse("#F8E3A1"),
        BodyFont = FontFamily.Default,
        MonoFont = Mono,
        FontSize = 15,
        LineHeight = 22,
        Padding = new Avalonia.Thickness(18, 16),
    };

    public static PreviewTheme For(Models.MarkdownFlavor flavor) =>
        flavor == Models.MarkdownFlavor.Discord ? Discord : GitHub;
}
