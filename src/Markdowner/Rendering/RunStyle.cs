using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Markdowner.Rendering;

/// <summary>
/// Inline formatting accumulated while walking down a document tree. Passed by
/// value so nested emphasis composes without any explicit push/pop bookkeeping.
/// </summary>
public readonly record struct RunStyle
{
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool Strikethrough { get; init; }
    public IBrush? Foreground { get; init; }
    public IBrush? Background { get; init; }
    public FontFamily? Font { get; init; }
    public double? FontSize { get; init; }

    public void ApplyTo(Run run)
    {
        if (Bold) run.FontWeight = FontWeight.Bold;
        if (Italic) run.FontStyle = FontStyle.Italic;
        if (Foreground is not null) run.Foreground = Foreground;
        if (Background is not null) run.Background = Background;
        if (Font is not null) run.FontFamily = Font;
        if (FontSize is { } size) run.FontSize = size;

        if (!Underline && !Strikethrough) return;

        var decorations = new TextDecorationCollection();
        if (Underline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (Strikethrough) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        run.TextDecorations = decorations;
    }
}
