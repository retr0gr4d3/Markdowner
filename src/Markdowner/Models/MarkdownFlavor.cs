namespace Markdowner.Models;

/// <summary>
/// The target platform whose Markdown dialect is being authored. The two
/// dialects diverge enough (Discord has no tables/images/HTML; GitHub has no
/// spoilers/underline/subtext) that the editor swaps both its rendering
/// pipeline and its insert palette when this changes.
/// </summary>
public enum MarkdownFlavor
{
    GitHub,
    Discord,
}

public static class MarkdownFlavorInfo
{
    public static string DisplayName(this MarkdownFlavor flavor) => flavor switch
    {
        MarkdownFlavor.GitHub => "GitHub",
        MarkdownFlavor.Discord => "Discord",
        _ => flavor.ToString(),
    };

    public static string Description(this MarkdownFlavor flavor) => flavor switch
    {
        MarkdownFlavor.GitHub => "GitHub Flavored Markdown + allow-listed HTML",
        MarkdownFlavor.Discord => "Discord message markdown",
        _ => string.Empty,
    };

    /// <summary>Discord rejects messages over 2000 characters; GitHub has no practical limit.</summary>
    public static int? CharacterLimit(this MarkdownFlavor flavor) => flavor switch
    {
        MarkdownFlavor.Discord => 2000,
        _ => null,
    };
}
