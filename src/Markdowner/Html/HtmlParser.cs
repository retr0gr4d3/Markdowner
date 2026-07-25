using System.Net;
using System.Text;

namespace Markdowner.Html;

/// <summary>
/// A small, deliberately forgiving HTML parser — enough for the hand-written
/// HTML people embed in READMEs. It never throws on malformed input; unclosed
/// tags simply close at the end of their parent, matching what browsers do.
/// </summary>
public static class HtmlParser
{
    public static List<HtmlNode> Parse(string html)
    {
        var root = new HtmlElement("#root");
        var stack = new Stack<HtmlElement>();
        stack.Push(root);

        var pending = new StringBuilder();
        var i = 0;

        void FlushText()
        {
            if (pending.Length == 0) return;
            stack.Peek().Children.Add(new HtmlText(WebUtility.HtmlDecode(pending.ToString())));
            pending.Clear();
        }

        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                pending.Append(html[i]);
                i++;
                continue;
            }

            // Comments and doctypes never reach the rendered page.
            if (StartsWith(html, i, "<!--"))
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }

            if (StartsWith(html, i, "<!") || StartsWith(html, i, "<?"))
            {
                var end = html.IndexOf('>', i);
                i = end < 0 ? html.Length : end + 1;
                continue;
            }

            if (StartsWith(html, i, "</"))
            {
                var end = html.IndexOf('>', i);
                if (end < 0) { pending.Append(html[i]); i++; continue; }

                FlushText();
                Close(stack, html[(i + 2)..end].Trim());
                i = end + 1;
                continue;
            }

            if (i + 1 < html.Length && char.IsLetter(html[i + 1]))
            {
                var element = ParseOpenTag(html, i, out var selfClosing, out var next);
                if (element is null) { pending.Append(html[i]); i++; continue; }

                FlushText();

                if (HtmlSpec.AutoClose.TryGetValue(element.Tag, out var closes))
                {
                    while (stack.Count > 1 && closes.Contains(stack.Peek().Tag, StringComparer.OrdinalIgnoreCase))
                    {
                        stack.Pop();
                    }
                }

                stack.Peek().Children.Add(element);

                if (!selfClosing && !HtmlSpec.IsVoid(element.Tag))
                {
                    stack.Push(element);
                }

                // <script>/<style> hold raw text, not markup — skip to the close tag.
                if (element.Tag.Equals("script", StringComparison.OrdinalIgnoreCase) ||
                    element.Tag.Equals("style", StringComparison.OrdinalIgnoreCase))
                {
                    var close = html.IndexOf($"</{element.Tag}", next, StringComparison.OrdinalIgnoreCase);
                    if (close < 0)
                    {
                        i = html.Length;
                    }
                    else
                    {
                        var closeEnd = html.IndexOf('>', close);
                        i = closeEnd < 0 ? html.Length : closeEnd + 1;
                    }
                    if (stack.Count > 1 && stack.Peek() == element) stack.Pop();
                    continue;
                }

                i = next;
                continue;
            }

            pending.Append(html[i]);
            i++;
        }

        FlushText();
        return root.Children;
    }

    private static void Close(Stack<HtmlElement> stack, string tag)
    {
        // Only unwind if the tag is actually open, so stray </div>s are ignored.
        if (!stack.Any(e => e.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase))) return;

        while (stack.Count > 1)
        {
            var top = stack.Pop();
            if (top.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) return;
        }
    }

    private static HtmlElement? ParseOpenTag(string html, int start, out bool selfClosing, out int next)
    {
        selfClosing = false;
        next = start + 1;

        var i = start + 1;
        var nameStart = i;
        while (i < html.Length && (char.IsLetterOrDigit(html[i]) || html[i] is '-' or ':')) i++;
        if (i == nameStart) return null;

        var element = new HtmlElement(html[nameStart..i]);

        while (i < html.Length)
        {
            while (i < html.Length && char.IsWhiteSpace(html[i])) i++;
            if (i >= html.Length) break;

            if (html[i] == '>')
            {
                i++;
                break;
            }

            if (html[i] == '/')
            {
                selfClosing = true;
                i++;
                continue;
            }

            var attrStart = i;
            while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] is not ('=' or '>' or '/')) i++;
            if (i == attrStart) { i++; continue; }

            var name = html[attrStart..i];

            while (i < html.Length && char.IsWhiteSpace(html[i])) i++;

            var value = name;
            if (i < html.Length && html[i] == '=')
            {
                i++;
                while (i < html.Length && char.IsWhiteSpace(html[i])) i++;

                if (i < html.Length && (html[i] == '"' || html[i] == '\''))
                {
                    var quote = html[i];
                    i++;
                    var valueStart = i;
                    while (i < html.Length && html[i] != quote) i++;
                    value = html[valueStart..Math.Min(i, html.Length)];
                    if (i < html.Length) i++;
                }
                else
                {
                    var valueStart = i;
                    while (i < html.Length && !char.IsWhiteSpace(html[i]) && html[i] != '>') i++;
                    value = html[valueStart..i];
                }
            }

            element.Attributes[name] = WebUtility.HtmlDecode(value);
        }

        next = i;
        return element;
    }

    private static bool StartsWith(string s, int index, string value) =>
        index + value.Length <= s.Length && string.CompareOrdinal(s, index, value, 0, value.Length) == 0;
}
