namespace Markdowner.Models;

/// <summary>
/// The starter document. Deliberately a single file that exercises both
/// dialects at once, so switching the renderer on the fly shows the same source
/// interpreted two different ways.
/// </summary>
public static class SampleDocuments
{
    public const string Default =
        """
        # Markdowner

        One document, two renderers. Change the **Renderer** dropdown in the toolbar
        and watch this same source render the way GitHub would, or the way Discord
        would. The insert bar above changes to match.

        ## Understood by both

        *Italic*, **bold**, ***bold italic***, ~~strikethrough~~ and `inline code`.

        > A quoted line.

        - A bulleted item
          - Nested one level
        1. A numbered item

        ```csharp
        var renderer = new GitHubRenderer();
        var preview = renderer.Render(document.Text);
        ```

        A [masked link](https://example.com), a bare URL <https://example.com>, and
        emoji shortcodes :tada: :rocket: :fire:

        ## GitHub only

        Discord renders none of the following — it prints most of it literally.

        | Feature | GitHub | Discord |
        | :--- | :---: | :---: |
        | Tables | Yes | No |
        | Raw HTML | Yes | No |
        | Spoilers | No | Yes |

        - [x] Render GitHub Flavored Markdown
        - [ ] Render Discord message markdown

        > [!NOTE]
        > GitHub turns this into a callout. Discord shows a plain quote.

        #### Heading level 4 — GitHub goes to 6, Discord stops at 3

        Footnotes look like this.[^1] Press <kbd>Ctrl</kbd>+<kbd>S</kbd> to save.
        Water is H<sub>2</sub>O and ten squared is 10<sup>2</sup>.

        <details>
        <summary>A collapsible section</summary>

        Only GitHub renders the HTML. Anything outside its allow-list — a
        `<script>` tag, say — is reported as stripped rather than drawn.

        </details>

        [^1]: Discord has no footnotes, so this line just reads as text there.

        ## Discord only

        GitHub renders none of the following the way Discord does.

        -# Subtext is small and muted, and only Discord has it.

        __Underlined__ on Discord, but bold on GitHub. Click to reveal a
        ||hidden spoiler||, which GitHub prints with the pipes showing.

        Mentions become chips: <@123456789012345678>, <@&123456789012345678>
        and <#123456789012345678>. So does @everyone.

        Timestamps localise to the reader: <t:1735689600:F>, which is
        <t:1735689600:R>.

        >>> A block quote swallows every remaining line of the message, so it
        has to come last. GitHub has no equivalent and shows the three angle
        brackets as text.
        """;
}
