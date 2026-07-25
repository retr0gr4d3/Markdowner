namespace Markdowner.Discord;

// ------------------------------------------------------------------ inlines

public abstract class DInline;

public sealed class DText(string text) : DInline
{
    public string Text { get; } = text;
}

public enum DEmphasisKind
{
    Bold,
    Italic,
    Underline,
    Strikethrough,
    Spoiler,
}

public sealed class DEmphasis(DEmphasisKind kind, List<DInline> children) : DInline
{
    public DEmphasisKind Kind { get; } = kind;
    public List<DInline> Children { get; } = children;
}

public sealed class DCodeSpan(string code) : DInline
{
    public string Code { get; } = code;
}

public sealed class DLink(string url, List<DInline> children, bool suppressedEmbed = false) : DInline
{
    public string Url { get; } = url;
    public List<DInline> Children { get; } = children;
    public bool SuppressedEmbed { get; } = suppressedEmbed;
}

public enum DMentionKind
{
    User,
    Role,
    Channel,
    Everyone,
    Here,
    SlashCommand,
}

public sealed class DMention(DMentionKind kind, string display) : DInline
{
    public DMentionKind Kind { get; } = kind;

    /// <summary>What Discord would show in place of the raw id, e.g. <c>@user</c>.</summary>
    public string Display { get; } = display;
}

public sealed class DEmoji(string name, bool animated, bool custom) : DInline
{
    public string Name { get; } = name;
    public bool Animated { get; } = animated;
    public bool Custom { get; } = custom;
}

public sealed class DTimestamp(long unixSeconds, char style) : DInline
{
    public long UnixSeconds { get; } = unixSeconds;
    public char Style { get; } = style;
}

public sealed class DLineBreak : DInline;

// ------------------------------------------------------------------- blocks

public abstract class DBlock;

public sealed class DParagraph(List<DInline> inlines) : DBlock
{
    public List<DInline> Inlines { get; } = inlines;
}

public sealed class DHeading(int level, List<DInline> inlines) : DBlock
{
    /// <summary>1–3. Discord has no h4+.</summary>
    public int Level { get; } = level;
    public List<DInline> Inlines { get; } = inlines;
}

public sealed class DSubtext(List<DInline> inlines) : DBlock
{
    public List<DInline> Inlines { get; } = inlines;
}

public sealed class DQuote(List<DBlock> children) : DBlock
{
    public List<DBlock> Children { get; } = children;
}

public sealed class DCodeBlock(string language, string code) : DBlock
{
    public string Language { get; } = language;
    public string Code { get; } = code;
}

/// <summary>
/// A flat run of list items. Nesting is carried per-item as an indent level
/// rather than as a tree — Discord's own nesting is shallow and purely visual.
/// </summary>
public sealed class DList(bool ordered, List<DListItem> items) : DBlock
{
    public bool Ordered { get; } = ordered;
    public List<DListItem> Items { get; } = items;
}

public sealed class DListItem(int level, string marker, List<DInline> inlines)
{
    public int Level { get; } = level;
    public string Marker { get; } = marker;
    public List<DInline> Inlines { get; } = inlines;
}
