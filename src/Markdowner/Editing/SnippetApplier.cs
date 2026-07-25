using System.Text.RegularExpressions;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Markdowner.Models;

namespace Markdowner.Editing;

/// <summary>Applies an insert-bar <see cref="Snippet"/> to the document.</summary>
public static partial class SnippetApplier
{
    [GeneratedRegex(@"^\d+[.)] $")]
    private static partial Regex OrderedPrefixRe { get; }

    public static void Apply(TextEditor editor, Snippet snippet)
    {
        var document = editor.Document;
        if (document is null) return;

        document.BeginUpdate();
        try
        {
            switch (snippet.Kind)
            {
                case SnippetKind.Wrap:
                    ApplyWrap(editor, snippet);
                    break;
                case SnippetKind.LinePrefix:
                    ApplyLinePrefix(editor, snippet);
                    break;
                case SnippetKind.Block:
                    ApplyBlock(editor, snippet);
                    break;
                default:
                    ApplyInsert(editor, snippet);
                    break;
            }
        }
        finally
        {
            document.EndUpdate();
        }

        editor.Focus();
    }

    private static void ApplyWrap(TextEditor editor, Snippet snippet)
    {
        var length = editor.SelectionLength;
        var start = length > 0 ? editor.SelectionStart : editor.CaretOffset;
        var hadSelection = length > 0;

        var inner = hadSelection ? editor.Document.GetText(start, length) : snippet.Placeholder;
        var text = snippet.Before + inner + snippet.After;

        editor.Document.Replace(start, length, text);

        // With a selection, keep the whole thing selected; otherwise select the
        // placeholder so the next keystroke replaces it.
        if (hadSelection) editor.Select(start, text.Length);
        else editor.Select(start + snippet.Before.Length, inner.Length);
    }

    private static void ApplyInsert(TextEditor editor, Snippet snippet)
    {
        var length = editor.SelectionLength;
        var start = length > 0 ? editor.SelectionStart : editor.CaretOffset;

        editor.Document.Replace(start, length, snippet.Before);
        SelectPlaceholder(editor, start, snippet.Before, snippet.Placeholder);
    }

    private static void ApplyBlock(TextEditor editor, Snippet snippet)
    {
        var document = editor.Document;
        var caret = editor.SelectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
        var line = document.GetLineByOffset(caret);
        var lineText = document.GetText(line.Offset, line.Length);

        var onBlankLine = lineText.Trim().Length == 0;

        // Block snippets always start their own line, and leave a blank line
        // behind them so following content is not absorbed into the block.
        var offset = onBlankLine ? line.Offset : line.EndOffset;
        var replaceLength = onBlankLine ? line.Length : 0;

        var leading = onBlankLine ? string.Empty : "\n\n";
        var trailing = HasContentAfter(document, line) ? "\n" : string.Empty;
        var text = leading + snippet.Before + trailing;

        document.Replace(offset, replaceLength, text);
        SelectPlaceholder(editor, offset, text, snippet.Placeholder);
    }

    private static bool HasContentAfter(TextDocument document, DocumentLine line) =>
        line.NextLine is { } next && document.GetText(next.Offset, next.Length).Trim().Length > 0;

    private static void ApplyLinePrefix(TextEditor editor, Snippet snippet)
    {
        var document = editor.Document;

        var selectionLength = editor.SelectionLength;
        var start = selectionLength > 0 ? editor.SelectionStart : editor.CaretOffset;
        var end = start + selectionLength;

        var firstLine = document.GetLineByOffset(start).LineNumber;
        var lastLine = document.GetLineByOffset(end).LineNumber;

        var prefix = snippet.Before;
        var stripper = snippet.Strip is null ? null : new Regex(snippet.Strip);

        // Toggle off when every touched line already carries this exact prefix.
        var allPrefixed = true;
        for (var number = firstLine; number <= lastLine; number++)
        {
            var line = document.GetLineByNumber(number);
            if (!document.GetText(line.Offset, line.Length).StartsWith(prefix, StringComparison.Ordinal))
            {
                allPrefixed = false;
                break;
            }
        }

        var ordered = OrderedPrefixRe.IsMatch(prefix);
        var counter = 1;

        // Rewrite bottom-up so earlier line offsets stay valid.
        var replacements = new List<(int Offset, int Length, string Text)>();
        for (var number = firstLine; number <= lastLine; number++)
        {
            var line = document.GetLineByNumber(number);
            var text = document.GetText(line.Offset, line.Length);

            string updated;
            if (allPrefixed)
            {
                updated = text[prefix.Length..];
            }
            else
            {
                var body = stripper?.Replace(text, string.Empty, 1) ?? text;
                if (body.Trim().Length == 0 && firstLine == lastLine) body = snippet.Placeholder;
                updated = (ordered ? $"{counter++}. " : prefix) + body;
            }

            replacements.Add((line.Offset, line.Length, updated));
        }

        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var (offset, length, text) = replacements[i];
            document.Replace(offset, length, text);
        }

        var newStart = document.GetLineByNumber(firstLine).Offset;
        var newEnd = document.GetLineByNumber(lastLine).EndOffset;

        if (selectionLength > 0)
        {
            editor.Select(newStart, newEnd - newStart);
        }
        else
        {
            var lineText = document.GetText(newStart, newEnd - newStart);
            SelectPlaceholder(editor, newStart, lineText, snippet.Placeholder);
        }
    }

    /// <summary>Selects the placeholder inside freshly inserted text, or parks the caret after it.</summary>
    private static void SelectPlaceholder(TextEditor editor, int offset, string inserted, string placeholder)
    {
        if (placeholder.Length > 0)
        {
            var index = inserted.IndexOf(placeholder, StringComparison.Ordinal);
            if (index >= 0)
            {
                editor.Select(offset + index, placeholder.Length);
                return;
            }
        }

        editor.CaretOffset = Math.Min(offset + inserted.Length, editor.Document.TextLength);
        editor.SelectionLength = 0;
    }
}
