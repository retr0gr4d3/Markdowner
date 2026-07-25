using System.Text;
using System.Text.RegularExpressions;

namespace Markdowner.Discord;

/// <summary>
/// A parser for Discord's message markdown.
/// <para>
/// Discord is deliberately *not* CommonMark, so Markdig would give the wrong
/// answer in several visible ways. The differences this parser exists to honour:
/// </para>
/// <list type="bullet">
///   <item><description><c>__text__</c> is an underline, not bold.</description></item>
///   <item><description>A single newline is a hard line break — paragraphs are not reflowed.</description></item>
///   <item><description><c>||text||</c> is a spoiler; <c>-# text</c> is subtext.</description></item>
///   <item><description>Headings stop at level 3; there are no tables, images, footnotes or raw HTML.</description></item>
///   <item><description><c>&gt;&gt;&gt;</c> quotes every remaining line of the message.</description></item>
/// </list>
/// </summary>
public static partial class DiscordParser
{
    [GeneratedRegex(@"^(#{1,3})[ \t]+(.*)$")]
    private static partial Regex HeadingRe { get; }

    [GeneratedRegex(@"^-#[ \t]+(.*)$")]
    private static partial Regex SubtextRe { get; }

    [GeneratedRegex(@"^([ \t]*)(?:([-*])|(\d{1,9})[.)])[ \t]+(.*)$")]
    private static partial Regex ListItemRe { get; }

    [GeneratedRegex(@"^\s*```(\S*)\s*$")]
    private static partial Regex FenceOpenRe { get; }

    [GeneratedRegex(@"^\s*```\s*$")]
    private static partial Regex FenceCloseRe { get; }

    [GeneratedRegex(@"\G<@!?(\d{1,25})>")]
    private static partial Regex UserMentionRe { get; }

    [GeneratedRegex(@"\G<@&(\d{1,25})>")]
    private static partial Regex RoleMentionRe { get; }

    [GeneratedRegex(@"\G<#(\d{1,25})>")]
    private static partial Regex ChannelMentionRe { get; }

    [GeneratedRegex(@"\G<(a)?:([A-Za-z0-9_]{2,32}):(\d{1,25})>")]
    private static partial Regex CustomEmojiRe { get; }

    [GeneratedRegex(@"\G<t:(-?\d{1,15})(?::([tTdDfFR]))?>")]
    private static partial Regex TimestampRe { get; }

    [GeneratedRegex(@"\G</([\w -]{1,64}):(\d{1,25})>")]
    private static partial Regex SlashCommandRe { get; }

    [GeneratedRegex(@"\G<(https?://[^\s<>]+)>")]
    private static partial Regex SuppressedLinkRe { get; }

    [GeneratedRegex(@"\G:([a-z0-9_+-]*[a-z][a-z0-9_+-]*):", RegexOptions.IgnoreCase)]
    private static partial Regex ShortcodeRe { get; }

    [GeneratedRegex(@"\Ghttps?://[^\s<>\[\]()]+")]
    private static partial Regex BareUrlRe { get; }

    // ============================================================== blocks

    public static List<DBlock> Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return ParseBlocks(lines, 0, lines.Length);
    }

    private static List<DBlock> ParseBlocks(string[] lines, int start, int end)
    {
        var blocks = new List<DBlock>();
        var i = start;

        while (i < end)
        {
            var line = lines[i];

            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            // ``` fenced code ```
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                i = ReadFence(lines, i, end, blocks);
                continue;
            }

            // >>> quotes everything that follows, to the end of the message.
            if (line.StartsWith(">>>", StringComparison.Ordinal))
            {
                var inner = new string[end - i];
                inner[0] = StripOneSpace(line[3..]);
                for (var k = i + 1; k < end; k++) inner[k - i] = lines[k];
                blocks.Add(new DQuote(ParseBlocks(inner, 0, inner.Length)));
                return blocks;
            }

            // > single-line quote; consecutive quoted lines merge into one block.
            if (line.StartsWith('>') && (line.Length == 1 || line[1] != '>'))
            {
                var quoted = new List<string>();
                while (i < end && lines[i].StartsWith('>') && (lines[i].Length == 1 || lines[i][1] != '>'))
                {
                    quoted.Add(StripOneSpace(lines[i][1..]));
                    i++;
                }
                var arr = quoted.ToArray();
                blocks.Add(new DQuote(ParseBlocks(arr, 0, arr.Length)));
                continue;
            }

            var heading = HeadingRe.Match(line);
            if (heading.Success)
            {
                blocks.Add(new DHeading(heading.Groups[1].Value.Length, ParseInlines(heading.Groups[2].Value)));
                i++;
                continue;
            }

            var subtext = SubtextRe.Match(line);
            if (subtext.Success)
            {
                blocks.Add(new DSubtext(ParseInlines(subtext.Groups[1].Value)));
                i++;
                continue;
            }

            if (ListItemRe.IsMatch(line))
            {
                i = ReadList(lines, i, end, blocks);
                continue;
            }

            // Anything else is a paragraph. Consecutive plain lines stay in one
            // paragraph but keep their hard breaks, matching Discord.
            var para = new List<string>();
            while (i < end)
            {
                var l = lines[i];
                if (l.Trim().Length == 0) break;
                if (l.TrimStart().StartsWith("```", StringComparison.Ordinal)) break;
                if (l.StartsWith('>')) break;
                if (HeadingRe.IsMatch(l) || SubtextRe.IsMatch(l) || ListItemRe.IsMatch(l)) break;
                para.Add(l);
                i++;
            }

            blocks.Add(new DParagraph(ParseInlines(string.Join('\n', para))));
        }

        return blocks;
    }

    private static int ReadFence(string[] lines, int i, int end, List<DBlock> blocks)
    {
        var raw = lines[i].TrimStart();

        // A fence that opens and closes on one line, e.g. ```code```.
        if (raw.Length > 6 && raw.EndsWith("```", StringComparison.Ordinal) && !FenceOpenRe.IsMatch(lines[i]))
        {
            blocks.Add(new DCodeBlock(string.Empty, raw[3..^3]));
            return i + 1;
        }

        var open = FenceOpenRe.Match(lines[i]);
        var language = open.Success ? open.Groups[1].Value : string.Empty;

        // Discord treats the token after ``` as a language only when the block
        // spans lines; otherwise it is just the first word of the code.
        var body = new List<string>();
        var j = i + 1;
        var closed = false;
        while (j < end)
        {
            if (FenceCloseRe.IsMatch(lines[j])) { closed = true; break; }
            body.Add(lines[j]);
            j++;
        }

        if (!closed)
        {
            // Unterminated fence: Discord shows the backticks literally.
            blocks.Add(new DParagraph(ParseInlines(string.Join('\n', lines[i..end]))));
            return end;
        }

        blocks.Add(new DCodeBlock(language, string.Join('\n', body)));
        return j + 1;
    }

    private static int ReadList(string[] lines, int i, int end, List<DBlock> blocks)
    {
        var items = new List<DListItem>();
        bool? ordered = null;

        while (i < end)
        {
            var m = ListItemRe.Match(lines[i]);
            if (!m.Success) break;

            var isOrdered = m.Groups[3].Success;
            ordered ??= isOrdered;
            if (isOrdered != ordered) break;

            var indent = m.Groups[1].Value.Replace("\t", "  ").Length;
            var level = Math.Min(indent / 2, 6);
            var marker = isOrdered ? m.Groups[3].Value + "." : "•";

            items.Add(new DListItem(level, marker, ParseInlines(m.Groups[4].Value)));
            i++;
        }

        blocks.Add(new DList(ordered ?? false, items));
        return i;
    }

    private static string StripOneSpace(string s) =>
        s.StartsWith(' ') ? s[1..] : s;

    // ============================================================== inlines

    public static List<DInline> ParseInlines(string s)
    {
        var result = new List<DInline>();
        var pending = new StringBuilder();
        var i = 0;

        void Flush()
        {
            if (pending.Length == 0) return;
            result.Add(new DText(pending.ToString()));
            pending.Clear();
        }

        while (i < s.Length)
        {
            var c = s[i];

            if (c == '\\' && i + 1 < s.Length && IsEscapable(s[i + 1]))
            {
                pending.Append(s[i + 1]);
                i += 2;
                continue;
            }

            if (c == '\n')
            {
                Flush();
                result.Add(new DLineBreak());
                i++;
                continue;
            }

            if (c == '`')
            {
                var run = RunLength(s, i, '`');
                var close = FindCodeClose(s, i + run, run);
                if (close > 0)
                {
                    Flush();
                    result.Add(new DCodeSpan(s[(i + run)..close].Trim('\n')));
                    i = close + run;
                    continue;
                }
            }

            if (c is '*' or '_' or '~' or '|')
            {
                var consumed = TryEmphasis(s, i, result, Flush);
                if (consumed > 0) { i += consumed; continue; }
            }

            if (c == '[')
            {
                var consumed = TryMaskedLink(s, i, result, Flush);
                if (consumed > 0) { i += consumed; continue; }
            }

            if (c == '<')
            {
                var consumed = TryAngleForm(s, i, result, Flush);
                if (consumed > 0) { i += consumed; continue; }
            }

            if (c == ':')
            {
                var m = ShortcodeRe.Match(s, i);
                if (m.Success)
                {
                    Flush();
                    result.Add(new DEmoji(m.Groups[1].Value, animated: false, custom: false));
                    i += m.Length;
                    continue;
                }
            }

            if (c == '@')
            {
                if (string.CompareOrdinal(s, i, "@everyone", 0, 9) == 0)
                {
                    Flush();
                    result.Add(new DMention(DMentionKind.Everyone, "@everyone"));
                    i += 9;
                    continue;
                }
                if (string.CompareOrdinal(s, i, "@here", 0, 5) == 0)
                {
                    Flush();
                    result.Add(new DMention(DMentionKind.Here, "@here"));
                    i += 5;
                    continue;
                }
            }

            if (c is 'h' or 'H')
            {
                var m = BareUrlRe.Match(s, i);
                if (m.Success)
                {
                    var url = TrimUrlTail(m.Value);
                    Flush();
                    result.Add(new DLink(url, [new DText(url)]));
                    i += url.Length;
                    continue;
                }
            }

            pending.Append(c);
            i++;
        }

        Flush();
        return result;
    }

    /// <summary>Returns the number of characters consumed, or 0 if this is not emphasis.</summary>
    private static int TryEmphasis(string s, int i, List<DInline> result, Action flush)
    {
        var c = s[i];
        var run = RunLength(s, i, c);

        // Longest-first so ***x*** beats **x** beats *x*.
        int[] widths = c switch
        {
            '*' => [3, 2, 1],
            '_' => [2, 1],
            '~' => [2],
            '|' => [2],
            _ => [],
        };

        foreach (var width in widths)
        {
            if (run < width) continue;

            var delim = new string(c, width);
            var contentStart = i + width;
            var close = FindClosingDelimiter(s, contentStart, delim);
            if (close < 0 || close == contentStart) continue;

            // Underscores must sit on word boundaries so snake_case_names survive.
            if (c == '_' && !IsWordBoundary(s, i, close + width)) continue;

            var inner = ParseInlines(s[contentStart..close]);
            flush();

            if (c == '*' && width == 3)
            {
                result.Add(new DEmphasis(DEmphasisKind.Bold,
                    [new DEmphasis(DEmphasisKind.Italic, inner)]));
            }
            else
            {
                var kind = (c, width) switch
                {
                    ('*', 2) => DEmphasisKind.Bold,
                    ('*', 1) => DEmphasisKind.Italic,
                    ('_', 2) => DEmphasisKind.Underline,
                    ('_', 1) => DEmphasisKind.Italic,
                    ('~', 2) => DEmphasisKind.Strikethrough,
                    _ => DEmphasisKind.Spoiler,
                };
                result.Add(new DEmphasis(kind, inner));
            }

            return close + width - i;
        }

        return 0;
    }

    private static int TryMaskedLink(string s, int i, List<DInline> result, Action flush)
    {
        var labelEnd = FindClosingDelimiter(s, i + 1, "]");
        if (labelEnd < 0 || labelEnd + 1 >= s.Length || s[labelEnd + 1] != '(') return 0;

        var urlEnd = s.IndexOf(')', labelEnd + 2);
        if (urlEnd < 0) return 0;

        var url = s[(labelEnd + 2)..urlEnd].Trim();
        if (url.Length == 0 || url.Contains('\n')) return 0;

        flush();
        result.Add(new DLink(url, ParseInlines(s[(i + 1)..labelEnd])));
        return urlEnd + 1 - i;
    }

    private static int TryAngleForm(string s, int i, List<DInline> result, Action flush)
    {
        Match m;

        if ((m = UserMentionRe.Match(s, i)).Success)
        {
            flush();
            result.Add(new DMention(DMentionKind.User, "@user"));
            return m.Length;
        }
        if ((m = RoleMentionRe.Match(s, i)).Success)
        {
            flush();
            result.Add(new DMention(DMentionKind.Role, "@role"));
            return m.Length;
        }
        if ((m = ChannelMentionRe.Match(s, i)).Success)
        {
            flush();
            result.Add(new DMention(DMentionKind.Channel, "#channel"));
            return m.Length;
        }
        if ((m = SlashCommandRe.Match(s, i)).Success)
        {
            flush();
            result.Add(new DMention(DMentionKind.SlashCommand, "/" + m.Groups[1].Value));
            return m.Length;
        }
        if ((m = CustomEmojiRe.Match(s, i)).Success)
        {
            flush();
            result.Add(new DEmoji(m.Groups[2].Value, animated: m.Groups[1].Success, custom: true));
            return m.Length;
        }
        if ((m = TimestampRe.Match(s, i)).Success)
        {
            var style = m.Groups[2].Success ? m.Groups[2].Value[0] : 'f';
            if (long.TryParse(m.Groups[1].Value, out var unix))
            {
                flush();
                result.Add(new DTimestamp(unix, style));
                return m.Length;
            }
        }
        if ((m = SuppressedLinkRe.Match(s, i)).Success)
        {
            var url = m.Groups[1].Value;
            flush();
            result.Add(new DLink(url, [new DText(url)], suppressedEmbed: true));
            return m.Length;
        }

        return 0;
    }

    // --------------------------------------------------------------- scanning

    private static int RunLength(string s, int i, char c)
    {
        var n = 0;
        while (i + n < s.Length && s[i + n] == c) n++;
        return n;
    }

    /// <summary>Finds <paramref name="delim"/> at or after <paramref name="from"/>, skipping escapes.</summary>
    private static int FindClosingDelimiter(string s, int from, string delim)
    {
        for (var i = from; i <= s.Length - delim.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (string.CompareOrdinal(s, i, delim, 0, delim.Length) == 0) return i;
        }
        return -1;
    }

    /// <summary>Code spans ignore escapes, so they get their own scan.</summary>
    private static int FindCodeClose(string s, int from, int run)
    {
        for (var i = from; i <= s.Length - run; i++)
        {
            if (s[i] != '`') continue;
            if (RunLength(s, i, '`') >= run) return i;
        }
        return -1;
    }

    private static bool IsWordBoundary(string s, int openStart, int closeEnd)
    {
        var before = openStart > 0 ? s[openStart - 1] : ' ';
        var after = closeEnd < s.Length ? s[closeEnd] : ' ';
        return !char.IsLetterOrDigit(before) && !char.IsLetterOrDigit(after);
    }

    private static bool IsEscapable(char c) =>
        !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c);

    /// <summary>Drops sentence punctuation that a bare URL swallowed.</summary>
    private static string TrimUrlTail(string url)
    {
        while (url.Length > 0 && ".,!?;:".Contains(url[^1])) url = url[..^1];
        return url;
    }
}
