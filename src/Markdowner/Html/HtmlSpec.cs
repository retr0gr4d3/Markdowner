namespace Markdowner.Html;

/// <summary>
/// GitHub renders Markdown to HTML and then runs it through a sanitizer with a
/// fixed allow-list. These sets mirror that behaviour so the preview shows what
/// GitHub would actually publish — including silently dropping what it strips.
/// </summary>
public static class HtmlSpec
{
    /// <summary>Tags that survive GitHub's sanitizer.</summary>
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "aside", "audio", "b", "bdo", "blockquote", "br", "caption", "cite",
        "code", "col", "colgroup", "dd", "del", "details", "dfn", "div", "dl", "dt", "em",
        "figcaption", "figure", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img",
        "ins", "kbd", "li", "mark", "ol", "p", "picture", "pre", "q", "rp", "rt", "ruby",
        "s", "samp", "small", "source", "span", "strike", "strong", "sub", "summary",
        "sup", "table", "tbody", "td", "tfoot", "th", "thead", "time", "tr", "track",
        "tt", "ul", "var", "video", "wbr",
    };

    /// <summary>Tags GitHub removes outright, content and all.</summary>
    public static readonly HashSet<string> Stripped = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "frame", "frameset", "object", "embed", "applet",
        "form", "button", "input", "textarea", "select", "option", "meta", "link",
        "base", "title", "head", "noscript", "canvas", "svg", "math",
    };

    public static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link",
        "meta", "param", "source", "track", "wbr",
    };

    /// <summary>Elements laid out as blocks rather than inline runs.</summary>
    public static readonly HashSet<string> Block = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "caption", "colgroup", "dd",
        "details", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer",
        "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main",
        "nav", "ol", "p", "picture", "pre", "section", "summary", "table", "tbody",
        "td", "tfoot", "th", "thead", "tr", "ul", "video",
    };

    /// <summary>Tags that implicitly close an open sibling of the same kind.</summary>
    public static readonly Dictionary<string, string[]> AutoClose = new(StringComparer.OrdinalIgnoreCase)
    {
        ["li"] = ["li"],
        ["dt"] = ["dt", "dd"],
        ["dd"] = ["dt", "dd"],
        ["tr"] = ["tr", "td", "th"],
        ["td"] = ["td", "th"],
        ["th"] = ["td", "th"],
        ["p"] = ["p"],
        ["thead"] = ["thead", "tbody", "tfoot"],
        ["tbody"] = ["thead", "tbody", "tfoot"],
        ["tfoot"] = ["thead", "tbody", "tfoot"],
    };

    public static bool IsBlock(string tag) => Block.Contains(tag);
    public static bool IsVoid(string tag) => Void.Contains(tag);
}
