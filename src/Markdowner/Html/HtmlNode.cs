namespace Markdowner.Html;

public abstract class HtmlNode;

public sealed class HtmlText(string text) : HtmlNode
{
    public string Text { get; } = text;
}

public sealed class HtmlElement(string tag) : HtmlNode
{
    public string Tag { get; } = tag;
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<HtmlNode> Children { get; } = [];

    public string? Attribute(string name) =>
        Attributes.TryGetValue(name, out var value) ? value : null;
}
