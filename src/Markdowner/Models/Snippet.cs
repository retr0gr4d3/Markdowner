namespace Markdowner.Models;

public enum SnippetKind
{
    /// <summary>Surround the selection (or <see cref="Snippet.Placeholder"/>) with Before/After.</summary>
    Wrap,

    /// <summary>Toggle a prefix onto every line touched by the selection.</summary>
    LinePrefix,

    /// <summary>Insert literal text, forced onto its own line and padded with blank lines.</summary>
    Block,

    /// <summary>Insert literal text at the caret, inline.</summary>
    Insert,
}

/// <summary>
/// One button in the insert bar. A snippet is purely declarative — how it is
/// applied to the document lives in <c>Markdowner.Editing.SnippetApplier</c>.
/// </summary>
/// <param name="Label">Button face. Kept very short; the insert bar is dense.</param>
/// <param name="Tooltip">Long-form explanation, shown on hover.</param>
/// <param name="Before">Wrap: opening text. Block/Insert: the whole literal.</param>
/// <param name="After">Wrap: closing text.</param>
/// <param name="Placeholder">
/// Text substituted when there is no selection, and re-selected afterwards so
/// the user can type straight over it.
/// </param>
/// <param name="Strip">
/// Regex matching a same-family prefix to remove before applying a
/// <see cref="SnippetKind.LinePrefix"/> — so H2 replaces H1 rather than stacking.
/// </param>
/// <param name="Mono">Render the button face in the monospace UI font (used for raw HTML tags).</param>
public sealed record Snippet(
    string Label,
    string Tooltip,
    SnippetKind Kind,
    string Before = "",
    string After = "",
    string Placeholder = "",
    string? Strip = null,
    bool Mono = false,
    string? Gesture = null);

/// <summary>A visually separated cluster of buttons inside a category.</summary>
public sealed record SnippetGroup(string Name, IReadOnlyList<Snippet> Snippets);

/// <summary>A tab in the insert bar.</summary>
public sealed record SnippetCategory(string Name, IReadOnlyList<SnippetGroup> Groups);
