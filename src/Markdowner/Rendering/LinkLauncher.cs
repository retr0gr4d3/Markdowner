using Avalonia;
using Avalonia.Controls;

namespace Markdowner.Rendering;

public static class LinkLauncher
{
    /// <summary>
    /// Opens a link from the preview in the user's browser. Markdown being
    /// previewed can come from anywhere, so only web-ish schemes are honoured —
    /// a document should not be able to launch <c>file:</c> targets on click.
    /// </summary>
    public static void Open(Visual source, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is not ("http" or "https" or "mailto")) return;

        _ = TopLevel.GetTopLevel(source)?.Launcher.LaunchUriAsync(uri);
    }
}
