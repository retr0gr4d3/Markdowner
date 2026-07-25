namespace Markdowner.Models;

/// <summary>
/// The insert-bar contents for each flavor. Only syntax the selected renderer
/// actually understands is offered — that is the whole point of the palette
/// swapping when you change flavor.
/// </summary>
public static class SnippetLibrary
{
    // Prefix families. Applying a member of a family replaces any existing
    // member on that line instead of stacking on top of it.
    private const string HeadingStrip = @"^#{1,6}[ \t]+";
    private const string DiscordHeadingStrip = @"^(?:#{1,3}|-#)[ \t]+";
    private const string ListStrip = @"^[ \t]*(?:[-*+]|\d+[.)])[ \t]+(?:\[[ xX]\][ \t]+)?";
    private const string QuoteStrip = @"^>{1,3}[ \t]?";

    public static IReadOnlyList<SnippetCategory> For(MarkdownFlavor flavor) => flavor switch
    {
        MarkdownFlavor.GitHub => GitHub,
        MarkdownFlavor.Discord => Discord,
        _ => Array.Empty<SnippetCategory>(),
    };

    // ---------------------------------------------------------------- helpers

    private static Snippet Wrap(string label, string tip, string before, string after,
        string placeholder, string? gesture = null) =>
        new(label, tip, SnippetKind.Wrap, before, after, placeholder, Gesture: gesture);

    private static Snippet Line(string label, string tip, string prefix, string? strip,
        string placeholder = "", string? gesture = null) =>
        new(label, tip, SnippetKind.LinePrefix, prefix, Placeholder: placeholder, Strip: strip, Gesture: gesture);

    private static Snippet Block(string label, string tip, string text, string placeholder = "") =>
        new(label, tip, SnippetKind.Block, text, Placeholder: placeholder);

    private static Snippet Insert(string label, string tip, string text, string placeholder = "") =>
        new(label, tip, SnippetKind.Insert, text, Placeholder: placeholder);

    /// <summary>A paired HTML tag from GitHub's sanitizer allow-list.</summary>
    private static Snippet Tag(string tag, string tip, string placeholder = "text") =>
        new($"<{tag}>", $"<{tag}> — {tip}", SnippetKind.Wrap, $"<{tag}>", $"</{tag}>", placeholder, Mono: true);

    /// <summary>A void (self-closing) HTML tag.</summary>
    private static Snippet VoidTag(string tag, string tip, string text) =>
        new($"<{tag}>", $"<{tag}> — {tip}", SnippetKind.Insert, text, Mono: true);

    // ----------------------------------------------------------------- GitHub

    private static readonly IReadOnlyList<SnippetCategory> GitHub =
    [
        new SnippetCategory("Text",
        [
            new SnippetGroup("Emphasis",
            [
                Wrap("B", "Bold — **text** (Ctrl+B)", "**", "**", "bold text", "Ctrl+B"),
                Wrap("I", "Italic — _text_ (Ctrl+I)", "_", "_", "italic text", "Ctrl+I"),
                Wrap("B/I", "Bold italic — ***text***", "***", "***", "bold italic text"),
                Wrap("S", "Strikethrough — ~~text~~", "~~", "~~", "struck text"),
                Wrap("</>", "Inline code — `code`", "`", "`", "code"),
            ]),
            new SnippetGroup("Script",
            [
                Wrap("x₂", "Subscript — <sub>text</sub>", "<sub>", "</sub>", "2"),
                Wrap("x²", "Superscript — <sup>text</sup>", "<sup>", "</sup>", "2"),
                Wrap("⌨", "Keyboard key — <kbd>Ctrl</kbd>", "<kbd>", "</kbd>", "Ctrl"),
            ]),
            new SnippetGroup("Escape",
            [
                Insert("\\", "Escape the next Markdown character", "\\"),
                Insert("<!--", "HTML comment — hidden from the rendered page", "<!-- comment -->", "comment"),
            ]),
        ]),

        new SnippetCategory("Blocks",
        [
            new SnippetGroup("Headings",
            [
                Line("H1", "Heading 1 — # (Ctrl+1)", "# ", HeadingStrip, "Heading", "Ctrl+1"),
                Line("H2", "Heading 2 — ## (Ctrl+2)", "## ", HeadingStrip, "Heading", "Ctrl+2"),
                Line("H3", "Heading 3 — ### (Ctrl+3)", "### ", HeadingStrip, "Heading", "Ctrl+3"),
                Line("H4", "Heading 4 — ####", "#### ", HeadingStrip, "Heading"),
                Line("H5", "Heading 5 — #####", "##### ", HeadingStrip, "Heading"),
                Line("H6", "Heading 6 — ######", "###### ", HeadingStrip, "Heading"),
            ]),
            new SnippetGroup("Structure",
            [
                Line("❝", "Blockquote — > text", "> ", QuoteStrip, "Quoted text"),
                Block("≡", "Fenced code block with a language hint",
                    """
                    ```csharp
                    Console.WriteLine("Hello");
                    ```
                    """, "csharp"),
                Block("—", "Horizontal rule", "---"),
            ]),
            new SnippetGroup("Alerts",
            [
                Block("Note", "GitHub alert — Note", "> [!NOTE]\n> Useful information a reader should know.",
                    "Useful information a reader should know."),
                Block("Tip", "GitHub alert — Tip", "> [!TIP]\n> Helpful advice for doing things better.",
                    "Helpful advice for doing things better."),
                Block("Important", "GitHub alert — Important", "> [!IMPORTANT]\n> Key information users need to know.",
                    "Key information users need to know."),
                Block("Warning", "GitHub alert — Warning", "> [!WARNING]\n> Urgent info needing immediate attention.",
                    "Urgent info needing immediate attention."),
                Block("Caution", "GitHub alert — Caution", "> [!CAUTION]\n> Advises about risks or negative outcomes.",
                    "Advises about risks or negative outcomes."),
            ]),
        ]),

        new SnippetCategory("Lists",
        [
            new SnippetGroup("Lists",
            [
                Line("•", "Bulleted list item", "- ", ListStrip, "List item"),
                Line("1.", "Numbered list item", "1. ", ListStrip, "List item"),
            ]),
            new SnippetGroup("Tasks",
            [
                Line("☐", "Task list item — unchecked", "- [ ] ", ListStrip, "To do"),
                Line("☑", "Task list item — done", "- [x] ", ListStrip, "Done"),
            ]),
            new SnippetGroup("Samples",
            [
                Block("Nested", "A nested bulleted list",
                    """
                    - First level
                      - Second level
                        - Third level
                    """, "First level"),
                Block("Checklist", "A task list",
                    """
                    - [x] Write the parser
                    - [ ] Write the renderer
                    - [ ] Ship it
                    """, "Write the parser"),
            ]),
        ]),

        new SnippetCategory("Insert",
        [
            new SnippetGroup("Links",
            [
                Wrap("Link", "Link — [text](url) (Ctrl+K)", "[", "](https://example.com)", "link text", "Ctrl+K"),
                Insert("Image", "Image — ![alt](url)", "![alt text](https://example.com/image.png)", "alt text"),
                Wrap("Auto", "Autolink — <https://example.com>", "<", ">", "https://example.com"),
                Block("Footnote", "Footnote reference and definition",
                    "Here is a statement with a footnote.[^1]\n\n[^1]: And here is the footnote itself.",
                    "And here is the footnote itself."),
            ]),
            new SnippetGroup("Tables",
            [
                Block("Table", "3-column table",
                    """
                    | Column A | Column B | Column C |
                    | --- | --- | --- |
                    | Cell | Cell | Cell |
                    """, "Column A"),
                Block("Aligned", "Table with per-column alignment",
                    """
                    | Left | Center | Right |
                    | :--- | :----: | ----: |
                    | a | b | c |
                    """, "Left"),
            ]),
            new SnippetGroup("GitHub",
            [
                Insert("@", "Mention a user or team", "@username", "username"),
                Insert("#", "Reference an issue or pull request", "#123", "123"),
                Insert(":☺:", "Emoji shortcode", ":sparkles:", "sparkles"),
                Block("Details", "Collapsible section",
                    """
                    <details>
                    <summary>Click to expand</summary>

                    Hidden content goes here.

                    </details>
                    """, "Click to expand"),
            ]),
            new SnippetGroup("Diagrams & math",
            [
                Block("Mermaid", "Mermaid diagram — rendered by GitHub",
                    """
                    ```mermaid
                    graph TD;
                        A[Start] --> B{Choice};
                        B -->|Yes| C[Do the thing];
                        B -->|No| D[Stop];
                    ```
                    """, "Start"),
                Wrap("$", "Inline LaTeX math", "$", "$", @"e^{i\pi}+1=0"),
                Block("$$", "Display LaTeX math", "$$\n\\int_0^\\infty e^{-x}\\,dx = 1\n$$",
                    @"\int_0^\infty e^{-x}\,dx = 1"),
            ]),
        ]),

        // GitHub runs Markdown output through an HTML sanitizer with a fixed
        // allow-list. Everything below survives it; anything not here (script,
        // style, iframe, form, button, ...) is stripped from the rendered page.
        new SnippetCategory("HTML",
        [
            new SnippetGroup("Inline text",
            [
                Tag("b", "bold, without added semantics"),
                Tag("strong", "strong importance"),
                Tag("i", "alternate voice"),
                Tag("em", "stress emphasis"),
                // No <u>: GitHub's sanitizer drops it. <ins> is the allowed underline.
                Tag("ins", "inserted text — renders underlined"),
                Tag("del", "deleted text"),
                Tag("s", "no longer accurate"),
                Tag("strike", "struck text (legacy)"),
                Tag("mark", "highlighted text"),
                Tag("small", "side comment / fine print"),
                Tag("sub", "subscript", "2"),
                Tag("sup", "superscript", "2"),
                Tag("span", "generic inline container"),
            ]),
            new SnippetGroup("Semantic",
            [
                Tag("code", "code fragment", "code"),
                Tag("samp", "sample program output", "output"),
                Tag("kbd", "keyboard input", "Ctrl"),
                Tag("var", "variable name", "x"),
                Tag("tt", "teletype text (legacy)"),
                Tag("abbr", "abbreviation", "HTML"),
                Tag("cite", "title of a work"),
                Tag("dfn", "the defining instance of a term"),
                Tag("q", "short inline quotation"),
                Tag("time", "machine-readable date/time", "2025-01-01"),
                Tag("bdo", "override text direction"),
                VoidTag("wbr", "optional word-break opportunity", "<wbr>"),
            ]),
            new SnippetGroup("Blocks",
            [
                Tag("p", "paragraph"),
                Tag("div", "generic block container"),
                Tag("blockquote", "quoted block"),
                Tag("pre", "preformatted text"),
                Tag("aside", "tangential content"),
                VoidTag("br", "line break", "<br>"),
                VoidTag("hr", "thematic break", "<hr>"),
            ]),
            new SnippetGroup("Headings",
            [
                Tag("h1", "heading level 1", "Heading"),
                Tag("h2", "heading level 2", "Heading"),
                Tag("h3", "heading level 3", "Heading"),
                Tag("h4", "heading level 4", "Heading"),
                Tag("h5", "heading level 5", "Heading"),
                Tag("h6", "heading level 6", "Heading"),
            ]),
            new SnippetGroup("Lists",
            [
                Tag("ul", "unordered list"),
                Tag("ol", "ordered list"),
                Tag("li", "list item"),
                Tag("dl", "description list"),
                Tag("dt", "description term"),
                Tag("dd", "description details"),
            ]),
            new SnippetGroup("Tables",
            [
                Block("<table>", "<table> — full HTML table skeleton",
                    """
                    <table>
                      <thead>
                        <tr><th>Column A</th><th>Column B</th></tr>
                      </thead>
                      <tbody>
                        <tr><td>Cell</td><td>Cell</td></tr>
                      </tbody>
                    </table>
                    """, "Column A") with { Mono = true },
                Tag("thead", "table header group"),
                Tag("tbody", "table body group"),
                Tag("tfoot", "table footer group"),
                Tag("tr", "table row"),
                Tag("th", "header cell"),
                Tag("td", "data cell"),
                Tag("caption", "table caption"),
                Tag("colgroup", "column group"),
                VoidTag("col", "column definition", "<col>"),
            ]),
            new SnippetGroup("Media",
            [
                VoidTag("img", "image", "<img src=\"https://example.com/image.png\" alt=\"alt text\" width=\"400\">"),
                Tag("picture", "art-directed image source set"),
                VoidTag("source", "media source alternative", "<source media=\"(prefers-color-scheme: dark)\" srcset=\"dark.png\">"),
                Tag("video", "video attachment"),
                Tag("figure", "self-contained figure"),
                Tag("figcaption", "figure caption"),
            ]),
            new SnippetGroup("Interactive",
            [
                Tag("details", "collapsible disclosure"),
                Tag("summary", "disclosure label", "Click to expand"),
                new Snippet("<a>", "<a> — hyperlink", SnippetKind.Wrap,
                    "<a href=\"https://example.com\">", "</a>", "link text", Mono: true),
                Tag("ruby", "ruby annotation"),
                Tag("rt", "ruby text"),
                Tag("rp", "ruby fallback parenthesis"),
            ]),
        ]),
    ];

    // ---------------------------------------------------------------- Discord

    private static readonly IReadOnlyList<SnippetCategory> Discord =
    [
        new SnippetCategory("Text",
        [
            new SnippetGroup("Emphasis",
            [
                Wrap("B", "Bold — **text** (Ctrl+B)", "**", "**", "bold text", "Ctrl+B"),
                Wrap("I", "Italic — *text* (Ctrl+I)", "*", "*", "italic text", "Ctrl+I"),
                Wrap("U", "Underline — __text__ (Ctrl+U)", "__", "__", "underlined text", "Ctrl+U"),
                Wrap("S", "Strikethrough — ~~text~~", "~~", "~~", "struck text"),
                Wrap("B/I", "Bold italic — ***text***", "***", "***", "bold italic text"),
            ]),
            new SnippetGroup("Hidden",
            [
                Wrap("||", "Spoiler — ||text|| (click to reveal)", "||", "||", "spoiler text"),
            ]),
            new SnippetGroup("Code",
            [
                Wrap("</>", "Inline code — `code`", "`", "`", "code"),
                Block("≡", "Fenced code block with syntax highlighting",
                    """
                    ```js
                    console.log("Hello");
                    ```
                    """, "js"),
            ]),
            new SnippetGroup("Escape",
            [
                Insert("\\", "Escape the next Markdown character", "\\"),
            ]),
        ]),

        new SnippetCategory("Blocks",
        [
            new SnippetGroup("Headings",
            [
                Line("H1", "Heading 1 — # (Ctrl+1)", "# ", DiscordHeadingStrip, "Heading", "Ctrl+1"),
                Line("H2", "Heading 2 — ## (Ctrl+2)", "## ", DiscordHeadingStrip, "Heading", "Ctrl+2"),
                Line("H3", "Heading 3 — ### (Ctrl+3)", "### ", DiscordHeadingStrip, "Heading", "Ctrl+3"),
                Line("-#", "Subtext — small muted text", "-# ", DiscordHeadingStrip, "Subtext"),
            ]),
            new SnippetGroup("Quotes",
            [
                Line("❝", "Single-line quote — > text", "> ", QuoteStrip, "Quoted text"),
                Block(">>>", "Block quote — quotes everything that follows",
                    ">>> Everything after this marker stays quoted.", "Everything after this marker stays quoted."),
            ]),
        ]),

        new SnippetCategory("Lists",
        [
            new SnippetGroup("Lists",
            [
                Line("•", "Bulleted list item", "- ", ListStrip, "List item"),
                Line("1.", "Numbered list item", "1. ", ListStrip, "List item"),
            ]),
            new SnippetGroup("Samples",
            [
                Block("Nested", "Nested list — Discord indents by two spaces",
                    """
                    - First level
                      - Second level
                        - Third level
                    """, "First level"),
            ]),
        ]),

        new SnippetCategory("Mentions",
        [
            new SnippetGroup("Mentions",
            [
                Insert("@user", "Mention a user — <@USER_ID>", "<@123456789012345678>", "123456789012345678"),
                Insert("@role", "Mention a role — <@&ROLE_ID>", "<@&123456789012345678>", "123456789012345678"),
                Insert("#channel", "Link a channel — <#CHANNEL_ID>", "<#123456789012345678>", "123456789012345678"),
            ]),
            new SnippetGroup("Broadcast",
            [
                Insert("@everyone", "Notify everyone in the server", "@everyone"),
                Insert("@here", "Notify online members in this channel", "@here"),
            ]),
            new SnippetGroup("Commands",
            [
                Insert("/cmd", "Slash-command mention — </name:ID>", "</command:123456789012345678>", "command"),
            ]),
        ]),

        new SnippetCategory("Insert",
        [
            new SnippetGroup("Links",
            [
                Wrap("Link", "Masked link — [text](url) (Ctrl+K)", "[", "](https://example.com)", "link text", "Ctrl+K"),
                Wrap("No embed", "Suppress the link preview — <https://example.com>", "<", ">", "https://example.com"),
            ]),
            new SnippetGroup("Emoji",
            [
                Insert(":☺:", "Unicode emoji shortcode", ":smile:", "smile"),
                Insert("<:e:>", "Custom server emoji — <:name:ID>", "<:emoji_name:123456789012345678>", "emoji_name"),
                Insert("<a:e:>", "Animated custom emoji — <a:name:ID>", "<a:emoji_name:123456789012345678>", "emoji_name"),
            ]),
            new SnippetGroup("Timestamps",
            [
                Insert("t", "Short time — 4:20 PM", "<t:1735689600:t>"),
                Insert("T", "Long time — 4:20:30 PM", "<t:1735689600:T>"),
                Insert("d", "Short date — 01/01/2025", "<t:1735689600:d>"),
                Insert("D", "Long date — January 1, 2025", "<t:1735689600:D>"),
                Insert("f", "Short date/time — January 1, 2025 4:20 PM", "<t:1735689600:f>"),
                Insert("F", "Long date/time — Wednesday, January 1, 2025 4:20 PM", "<t:1735689600:F>"),
                Insert("R", "Relative — in 3 hours", "<t:1735689600:R>"),
            ]),
        ]),
    ];
}
