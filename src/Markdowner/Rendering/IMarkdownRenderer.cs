using Avalonia.Controls;

namespace Markdowner.Rendering;

public interface IMarkdownRenderer
{
    PreviewTheme Theme { get; }

    /// <summary>Turns Markdown source into a live Avalonia control tree.</summary>
    Control Render(string markdown);
}
