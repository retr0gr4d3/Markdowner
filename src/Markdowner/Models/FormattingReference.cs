namespace Markdowner.Models;

/// <summary>
/// One documented construct. <see cref="Syntax"/> is real Markdown — the help
/// window feeds it through the live renderer, so the examples can never drift
/// away from what the preview actually does.
/// </summary>
public sealed record FormatEntry(string Name, string Syntax, string Notes = "");

public sealed record FormatSection(string Name, IReadOnlyList<FormatEntry> Entries);

public static class FormattingReference
{
    public static IReadOnlyList<FormatSection> For(MarkdownFlavor flavor) =>
        flavor == MarkdownFlavor.Discord ? Discord : GitHub;

    // ----------------------------------------------------------------- GitHub

    private static readonly IReadOnlyList<FormatSection> GitHub =
    [
        new FormatSection("Headings",
        [
            new FormatEntry("Heading 1", "# Heading 1", "Rendered with a rule beneath it."),
            new FormatEntry("Heading 2", "## Heading 2", "Also rendered with a rule."),
            new FormatEntry("Heading 3", "### Heading 3"),
            new FormatEntry("Headings 4–6", "#### Heading 4\n\n##### Heading 5\n\n###### Heading 6",
                "GitHub supports all six levels."),
        ]),

        new FormatSection("Text",
        [
            new FormatEntry("Bold", "**bold text**", "Or __bold text__ — on GitHub both mean bold."),
            new FormatEntry("Italic", "*italic text*", "Or _italic text_."),
            new FormatEntry("Bold italic", "***bold italic***"),
            new FormatEntry("Strikethrough", "~~struck through~~"),
            new FormatEntry("Inline code", "`inline code`"),
            new FormatEntry("Escaping", @"\*not italic\*", "A backslash makes the next character literal."),
        ]),

        new FormatSection("Blocks",
        [
            new FormatEntry("Blockquote", "> A quoted line.\n> Still quoted."),
            new FormatEntry("Fenced code", "```csharp\nvar x = 1;\n```",
                "The word after the fence is the language hint."),
            new FormatEntry("Horizontal rule", "---"),
            new FormatEntry("Line break", "First line  \nSecond line",
                "Two trailing spaces force a break inside a paragraph."),
        ]),

        new FormatSection("Lists",
        [
            new FormatEntry("Bulleted", "- First\n- Second\n  - Nested"),
            new FormatEntry("Numbered", "1. First\n2. Second"),
            new FormatEntry("Task list", "- [x] Done\n- [ ] Not done",
                "The checkbox replaces the bullet."),
        ]),

        new FormatSection("Links and media",
        [
            new FormatEntry("Link", "[link text](https://example.com)"),
            new FormatEntry("Autolink", "<https://example.com>", "A bare URL also links automatically."),
            new FormatEntry("Image", "![alt text](https://example.com/image.png)",
                "Markdowner shows a placeholder rather than fetching remote images."),
            new FormatEntry("Footnote", "A claim.[^ref]\n\n[^ref]: The supporting note.",
                "Footnotes are collected into a section at the end."),
        ]),

        new FormatSection("Tables",
        [
            new FormatEntry("Table", "| A | B |\n| --- | --- |\n| 1 | 2 |"),
            new FormatEntry("Column alignment", "| Left | Centre | Right |\n| :--- | :---: | ---: |\n| a | b | c |",
                "Colons in the delimiter row set alignment."),
        ]),

        new FormatSection("Alerts",
        [
            new FormatEntry("Note", "> [!NOTE]\n> Useful information a reader should know."),
            new FormatEntry("Tip", "> [!TIP]\n> Helpful advice."),
            new FormatEntry("Important", "> [!IMPORTANT]\n> Key information."),
            new FormatEntry("Warning", "> [!WARNING]\n> Needs immediate attention."),
            new FormatEntry("Caution", "> [!CAUTION]\n> Advises about risk."),
        ]),

        new FormatSection("HTML — text",
        [
            new FormatEntry("<b> / <strong>", "<b>bold</b> and <strong>strong</strong>"),
            new FormatEntry("<i> / <em>", "<i>italic</i> and <em>emphasis</em>"),
            new FormatEntry("<ins>", "<ins>inserted, shown underlined</ins>",
                "GitHub strips <u>, so <ins> is the way to underline."),
            new FormatEntry("<del> / <s>", "<del>deleted</del> and <s>struck</s>"),
            new FormatEntry("<mark>", "<mark>highlighted</mark>"),
            new FormatEntry("<small>", "<small>fine print</small>"),
            new FormatEntry("<sub> / <sup>", "H<sub>2</sub>O and 10<sup>2</sup>"),
            new FormatEntry("<kbd>", "Press <kbd>Ctrl</kbd>+<kbd>S</kbd>"),
            new FormatEntry("<code> / <samp> / <var>", "<code>code</code>, <samp>output</samp>, <var>x</var>"),
            new FormatEntry("<abbr>", "<abbr title=\"HyperText Markup Language\">HTML</abbr>"),
            new FormatEntry("<q>", "<q>a short quotation</q>"),
        ]),

        new FormatSection("HTML — structure",
        [
            new FormatEntry("<details> / <summary>",
                "<details>\n<summary>Click to expand</summary>\n\nHidden until opened.\n\n</details>",
                "Becomes a collapsible section."),
            new FormatEntry("<p> / <div>", "<div><p>A paragraph inside a div.</p></div>"),
            new FormatEntry("<blockquote>", "<blockquote>Quoted via HTML.</blockquote>"),
            new FormatEntry("<pre>", "<pre><code>preformatted\n  text</code></pre>"),
            new FormatEntry("<br> / <hr>", "line one<br>line two\n\n<hr>"),
            new FormatEntry("<ul> / <ol> / <li>", "<ul><li>one</li><li>two</li></ul>"),
            new FormatEntry("<dl> / <dt> / <dd>", "<dl><dt>Term</dt><dd>Definition</dd></dl>"),
            new FormatEntry("<table>",
                "<table>\n  <tr><th>A</th><th>B</th></tr>\n  <tr><td>1</td><td>2</td></tr>\n</table>",
                "colspan and rowspan are honoured."),
            new FormatEntry("<a>", "<a href=\"https://example.com\">an HTML link</a>"),
            new FormatEntry("<img>", "<img src=\"https://example.com/i.png\" alt=\"alt text\" width=\"400\">"),
            new FormatEntry("Stripped tags", "<script>alert(1)</script>",
                "Anything off GitHub's allow-list is reported instead of rendered."),
        ]),

        new FormatSection("GitHub extras",
        [
            new FormatEntry("Emoji shortcode", ":tada: :rocket: :fire:"),
            new FormatEntry("Mention", "@username", "Links to a user or team on github.com."),
            new FormatEntry("Issue reference", "#123", "Links to an issue or pull request."),
            new FormatEntry("Mermaid diagram", "```mermaid\ngraph TD;\n    A-->B;\n```",
                "GitHub draws the diagram; Markdowner shows the source and says so."),
            new FormatEntry("Inline maths", "$e^{i\\pi}+1=0$"),
            new FormatEntry("Display maths", "$$\n\\int_0^\\infty e^{-x}\\,dx = 1\n$$"),
            new FormatEntry("Comment", "<!-- invisible in the rendered page -->"),
        ]),
    ];

    // ---------------------------------------------------------------- Discord

    private static readonly IReadOnlyList<FormatSection> Discord =
    [
        new FormatSection("Headings",
        [
            new FormatEntry("Heading 1", "# Heading 1"),
            new FormatEntry("Heading 2", "## Heading 2"),
            new FormatEntry("Heading 3", "### Heading 3", "Discord stops here — there is no heading 4."),
            new FormatEntry("Subtext", "-# Small, muted subtext", "Discord only; GitHub has no equivalent."),
        ]),

        new FormatSection("Text",
        [
            new FormatEntry("Bold", "**bold text**"),
            new FormatEntry("Italic", "*italic text*", "Or _italic text_."),
            new FormatEntry("Underline", "__underlined text__",
                "Double underscore is underline on Discord, but bold on GitHub."),
            new FormatEntry("Bold italic", "***bold italic***"),
            new FormatEntry("Strikethrough", "~~struck through~~"),
            new FormatEntry("Spoiler", "||click to reveal||", "Hidden until clicked. Discord only."),
            new FormatEntry("Inline code", "`inline code`"),
            new FormatEntry("Escaping", @"\*not italic\*"),
        ]),

        new FormatSection("Blocks",
        [
            new FormatEntry("Fenced code", "```js\nconsole.log(1);\n```",
                "The language hint enables syntax colouring in Discord."),
            new FormatEntry("Quote", "> A single quoted line."),
            new FormatEntry("Block quote", ">>> Quotes every remaining line\nof the message.",
                "Everything after >>> stays quoted, so it belongs last."),
            new FormatEntry("Line break", "First line\nSecond line",
                "A single newline is a real break — Discord never reflows paragraphs."),
        ]),

        new FormatSection("Lists",
        [
            new FormatEntry("Bulleted", "- First\n- Second\n  - Nested"),
            new FormatEntry("Numbered", "1. First\n2. Second"),
        ]),

        new FormatSection("Links",
        [
            new FormatEntry("Masked link", "[link text](https://example.com)"),
            new FormatEntry("Bare URL", "https://example.com", "Links automatically and shows an embed."),
            new FormatEntry("Suppressed embed", "<https://example.com>",
                "Angle brackets keep the link but stop the preview embed."),
        ]),

        new FormatSection("Mentions",
        [
            new FormatEntry("User", "<@123456789012345678>"),
            new FormatEntry("Role", "<@&123456789012345678>"),
            new FormatEntry("Channel", "<#123456789012345678>"),
            new FormatEntry("Everyone / here", "@everyone and @here"),
            new FormatEntry("Slash command", "</deploy:123456789012345678>"),
        ]),

        new FormatSection("Emoji",
        [
            new FormatEntry("Unicode shortcode", ":tada: :rocket: :fire:"),
            new FormatEntry("Custom emoji", "<:emoji_name:123456789012345678>",
                "Server emoji cannot be resolved offline, so the code is shown."),
            new FormatEntry("Animated emoji", "<a:emoji_name:123456789012345678>"),
        ]),

        new FormatSection("Timestamps",
        [
            new FormatEntry("Short time", "<t:1735689600:t>", "Renders in each reader's own time zone."),
            new FormatEntry("Long time", "<t:1735689600:T>"),
            new FormatEntry("Short date", "<t:1735689600:d>"),
            new FormatEntry("Long date", "<t:1735689600:D>"),
            new FormatEntry("Short date/time", "<t:1735689600:f>", "The default when no style is given."),
            new FormatEntry("Long date/time", "<t:1735689600:F>"),
            new FormatEntry("Relative", "<t:1735689600:R>", "Counts up or down from now."),
        ]),

        new FormatSection("Not supported",
        [
            new FormatEntry("Tables, images, footnotes and HTML",
                "| A | B |\n| --- | --- |\n\n<kbd>Ctrl</kbd>\n\n![alt](https://example.com/i.png)",
                "Discord has none of these — it prints the source as written. "
                + "Switch the renderer to GitHub to see them rendered."),
        ]),
    ];
}
