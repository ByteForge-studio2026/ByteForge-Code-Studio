// Copyright (c) 2026 ByteForge
namespace ByteForge.Markdown;

/// <summary>
/// 轻量 Markdown 解析器：覆盖日常写作需要的语法
/// （标题、段落、围栏代码块、引用、有序 / 无序列表、分隔线、表格，
/// 以及行内的代码、加粗、斜体、链接）。
/// 不做完整 CommonMark 兼容——目标是"够用 + 不吃内存"。
/// </summary>
public static class MarkdownParser
{
    public static MarkdownDocument Parse(string text)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(text)) return new MarkdownDocument(blocks);

        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            string trimmed = line.Trim();

            // 空行
            if (trimmed.Length == 0) { i++; continue; }

            // 围栏代码块
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                string? language = trimmed.Length > 3 ? trimmed[3..].Trim() : null;
                if (string.IsNullOrEmpty(language)) language = null;

                i++;
                var code = new List<string>();
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    code.Add(lines[i]);
                    i++;
                }
                i++; // 跳过结束围栏
                blocks.Add(new CodeBlock(language, string.Join("\n", code)));
                continue;
            }

            // 分隔线
            if (trimmed is "---" or "***" or "___" || IsAllSame(trimmed, '-') || IsAllSame(trimmed, '*'))
            {
                blocks.Add(new HorizontalRuleBlock());
                i++;
                continue;
            }

            // 标题
            if (trimmed.StartsWith('#'))
            {
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;
                if (level <= 6 && level < trimmed.Length)
                {
                    blocks.Add(new HeadingBlock(level, ParseInline(trimmed[level..].Trim())));
                    i++;
                    continue;
                }
            }

            // 引用
            if (trimmed.StartsWith('>'))
            {
                var quoted = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                {
                    string content = lines[i].TrimStart();
                    content = content.StartsWith("> ") ? content[2..] : content.TrimStart('>');
                    quoted.Add(content);
                    i++;
                }
                blocks.Add(new QuoteBlock(Parse(string.Join("\n", quoted)).Blocks));
                continue;
            }

            // 表格（| a | b |）
            if (trimmed.Contains('|') && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var rows = new List<string[]> { SplitRow(trimmed) };
                i += 2;
                while (i < lines.Length && lines[i].Trim().Contains('|') && lines[i].Trim().Length > 0)
                {
                    rows.Add(SplitRow(lines[i].Trim()));
                    i++;
                }
                bool[] align = new bool[rows[0].Length];
                blocks.Add(new TableBlock(rows, align));
                continue;
            }

            // 列表
            if (TryParseListMarker(trimmed, out bool ordered, out int indent))
            {
                var items = new List<ListItemBlock>();
                while (i < lines.Length && TryParseListMarker(lines[i].Trim(), out bool o2, out int indent2))
                {
                    if (o2 != ordered) break;
                    string content = lines[i].Trim()[indent2..].Trim();
                    i++;
                    // 续行：缩进的内容并入当前项
                    while (i < lines.Length && lines[i].Trim().Length > 0 && !TryParseListMarker(lines[i].Trim(), out _, out _))
                    {
                        content += " " + lines[i].Trim();
                        i++;
                    }
                    items.Add(new ListItemBlock(ParseInline(content), 0));
                }
                blocks.Add(new ListBlock(ordered, items));
                continue;
            }

            // 段落（连续非空行合并）
            var paragraph = new List<string>();
            while (i < lines.Length)
            {
                string current = lines[i].Trim();
                if (current.Length == 0
                    || current.StartsWith('#')
                    || current.StartsWith('>')
                    || current.StartsWith("```", StringComparison.Ordinal))
                    break;
                paragraph.Add(current);
                i++;
            }
            if (paragraph.Count > 0)
                blocks.Add(new ParagraphBlock(ParseInline(string.Join(" ", paragraph))));
        }

        return new MarkdownDocument(blocks);
    }

    /// <summary>行内解析：`code`、**bold**、*em*、_em_、[text](url)。</summary>
    public static List<MarkdownInline> ParseInline(string text)
    {
        var result = new List<MarkdownInline>();
        if (string.IsNullOrEmpty(text)) return result;

        int i = 0;
        var buffer = new System.Text.StringBuilder();

        void Flush()
        {
            if (buffer.Length > 0)
            {
                result.Add(new TextInline(buffer.ToString()));
                buffer.Clear();
            }
        }

        while (i < text.Length)
        {
            char c = text[i];

            // 行内代码
            if (c == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    Flush();
                    result.Add(new CodeInline(text[(i + 1)..end]));
                    i = end + 1;
                    continue;
                }
            }

            // 链接
            if (c == '[')
            {
                int close = text.IndexOf(']', i + 1);
                if (close > i && close + 1 < text.Length && text[close + 1] == '(')
                {
                    int end = text.IndexOf(')', close + 2);
                    if (end > close)
                    {
                        Flush();
                        result.Add(new LinkInline(text[(i + 1)..close], text[(close + 2)..end]));
                        i = end + 1;
                        continue;
                    }
                }
            }

            // 加粗
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i + 1)
                {
                    Flush();
                    result.Add(new StrongInline(ParseInline(text[(i + 2)..end])));
                    i = end + 2;
                    continue;
                }
            }

            // 斜体
            if (c is '*' or '_')
            {
                int end = text.IndexOf(c, i + 1);
                if (end > i + 1)
                {
                    Flush();
                    result.Add(new EmphasisInline(ParseInline(text[(i + 1)..end])));
                    i = end + 1;
                    continue;
                }
            }

            buffer.Append(c);
            i++;
        }

        Flush();
        return result;
    }

    private static bool TryParseListMarker(string trimmed, out bool ordered, out int markerLength)
    {
        ordered = false;
        markerLength = 0;

        if (trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            markerLength = 2;
            return true;
        }

        int dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (dot > 0 && dot <= 3 && trimmed[..dot].All(char.IsDigit))
        {
            ordered = true;
            markerLength = dot + 2;
            return true;
        }

        return false;
    }

    private static bool IsTableSeparator(string line)
    {
        string trimmed = line.Trim();
        return trimmed.Contains('-') && trimmed.Contains('|') &&
               trimmed.All(ch => ch is '-' or '|' or ':' or ' ');
    }

    private static string[] SplitRow(string line) =>
        line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static bool IsAllSame(string value, char c) =>
        value.Length >= 3 && value.All(ch => ch == c);
}
