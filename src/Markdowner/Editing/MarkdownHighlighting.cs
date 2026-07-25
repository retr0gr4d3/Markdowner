using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using Markdowner.Models;

namespace Markdowner.Editing;

/// <summary>
/// Loads the source-pane highlighting for a flavor. The two definitions differ
/// on purpose: Discord colours spoilers, subtext and mentions, while GitHub
/// colours HTML tags, tables and footnotes.
/// </summary>
public static class MarkdownHighlighting
{
    private static readonly Dictionary<MarkdownFlavor, IHighlightingDefinition?> Cache = [];

    public static IHighlightingDefinition? For(MarkdownFlavor flavor)
    {
        if (Cache.TryGetValue(flavor, out var cached)) return cached;

        var definition = Load(flavor == MarkdownFlavor.Discord
            ? "Markdowner.Editing.Markdown-Discord.xshd"
            : "Markdowner.Editing.Markdown-GitHub.xshd");

        Cache[flavor] = definition;
        return definition;
    }

    private static IHighlightingDefinition? Load(string resourceName)
    {
        try
        {
            using var stream = typeof(MarkdownHighlighting).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;

            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception)
        {
            // A malformed definition should cost syntax colour, not the editor.
            return null;
        }
    }
}
