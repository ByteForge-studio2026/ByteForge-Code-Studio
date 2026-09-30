// Copyright (c) 2026 ByteForge
namespace ByteForge.Syntax;

/// <summary>
/// 逐行词法扫描器。
/// 之所以按行扫描：编辑器只需要在"打开文件时扫全文、编辑时重扫当前行"，
/// 既不占内存，也能保证输入时光标处立刻变色。
/// </summary>
public static class Tokenizer
{
    public static LineTokens TokenizeLine(string line, LanguageDefinition lang, LineState state)
    {
        if (lang.IsMarkdown) return TokenizeMarkdown(line, state);
        if (lang.IsMarkup) return TokenizeMarkup(line, state);
        return TokenizeCode(line, lang, state);
    }

    // ---------------------------------------------------------------- 代码语言

    private static LineTokens TokenizeCode(string line, LanguageDefinition lang, LineState state)
    {
        var tokens = new List<Token>();
        int i = 0;
        LineState next = state;

        // 续行的块注释
        if ((state & LineState.InBlockComment) != 0 && lang.BlockCommentEnd is { } blockEnd)
        {
            int end = line.IndexOf(blockEnd, StringComparison.Ordinal);
            if (end < 0)
            {
                Add(tokens, 0, line.Length, TokenKind.Comment);
                return new LineTokens(tokens, LineState.InBlockComment);
            }
            int stop = end + blockEnd.Length;
            Add(tokens, 0, stop, TokenKind.Comment);
            next &= ~LineState.InBlockComment;
            i = stop;
        }
        // 续行的三引号字符串
        else if ((state & LineState.InTripleString) != 0 && lang.TripleQuote is { } triple)
        {
            int end = line.IndexOf(triple, StringComparison.Ordinal);
            if (end < 0)
            {
                Add(tokens, 0, line.Length, TokenKind.String);
                return new LineTokens(tokens, LineState.InTripleString);
            }
            int stop = end + triple.Length;
            Add(tokens, 0, stop, TokenKind.String);
            next &= ~LineState.InTripleString;
            i = stop;
        }

        while (i < line.Length)
        {
            char c = line[i];

            if (char.IsWhiteSpace(c)) { i++; continue; }

            // 行注释
            if (lang.LineComment is { } lineComment && Matches(line, i, lineComment))
            {
                Add(tokens, i, line.Length - i, TokenKind.Comment);
                break;
            }

            // 块注释
            if (lang.BlockCommentStart is { } blockStart && lang.BlockCommentEnd is { } be &&
                Matches(line, i, blockStart))
            {
                int end = line.IndexOf(be, i + blockStart.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    Add(tokens, i, line.Length - i, TokenKind.Comment);
                    next |= LineState.InBlockComment;
                    break;
                }
                int stop = end + be.Length;
                Add(tokens, i, stop - i, TokenKind.Comment);
                i = stop;
                continue;
            }

            // 预处理指令 / 特性（#include、#region、#if）
            if (c == '#' && lang.SupportsPreprocessor && IsFirstNonSpace(line, i))
            {
                Add(tokens, i, line.Length - i, TokenKind.Preprocessor);
                break;
            }

            // 三引号字符串起始
            if (lang.TripleQuote is { } tq && Matches(line, i, tq))
            {
                int end = line.IndexOf(tq, i + tq.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    Add(tokens, i, line.Length - i, TokenKind.String);
                    next |= LineState.InTripleString;
                    break;
                }
                int stop = end + tq.Length;
                Add(tokens, i, stop - i, TokenKind.String);
                i = stop;
                continue;
            }

            // 普通字符串
            if (lang.QuoteChars.Contains(c))
            {
                i = ScanQuoted(line, i, c, TokenKind.String, tokens);
                continue;
            }

            // 字符字面量
            if (lang.SupportsCharLiteral && c == '\'')
            {
                i = ScanQuoted(line, i, '\'', TokenKind.Char, tokens);
                continue;
            }

            // 数字
            if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                i = ScanNumber(line, i, tokens);
                continue;
            }

            // 标识符 / 关键字 / 类型
            if (lang.IsIdentifierStart(c))
            {
                int start = i;
                while (i < line.Length && lang.IsIdentifierPart(line[i])) i++;
                string word = line[start..i];

                TokenKind kind = TokenKind.Identifier;
                if (lang.Keywords.Contains(word)) kind = TokenKind.Keyword;
                else if (lang.Types.Contains(word)) kind = TokenKind.Type;
                else if (i < line.Length && line[i] == '(' ) kind = TokenKind.Identifier;

                Add(tokens, start, i - start, kind);
                continue;
            }

            tokens.Add(new Token(i, 1, IsOperator(c) ? TokenKind.Operator : TokenKind.Punctuation));
            i++;
        }

        return new LineTokens(tokens, next);
    }

    // ---------------------------------------------------------------- 标记语言

    private static LineTokens TokenizeMarkup(string line, LineState state)
    {
        var tokens = new List<Token>();
        int i = 0;

        if ((state & LineState.InBlockComment) != 0)
        {
            int end = line.IndexOf("-->", StringComparison.Ordinal);
            if (end < 0)
            {
                Add(tokens, 0, line.Length, TokenKind.Comment);
                return new LineTokens(tokens, LineState.InBlockComment);
            }
            Add(tokens, 0, end + 3, TokenKind.Comment);
            i = end + 3;
        }

        while (i < line.Length)
        {
            char c = line[i];

            if (Matches(line, i, "<!--"))
            {
                int end = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
                if (end < 0)
                {
                    Add(tokens, i, line.Length - i, TokenKind.Comment);
                    return new LineTokens(tokens, LineState.InBlockComment);
                }
                Add(tokens, i, end + 3 - i, TokenKind.Comment);
                i = end + 3;
                continue;
            }

            if (c == '<' && i + 1 < line.Length && (char.IsLetter(line[i + 1]) || line[i + 1] == '/'))
            {
                Add(tokens, i, 1, TokenKind.XmlDelimiter);
                i++;
                if (line[i] == '/') { Add(tokens, i, 1, TokenKind.XmlDelimiter); i++; }

                // 标签名（可含命名空间前缀）
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '.' or ':' or '_' or '-')) i++;
                if (i > start) Add(tokens, start, i - start, TokenKind.Tag);

                // 标签内部：属性名 + 字符串
                while (i < line.Length && line[i] != '>')
                {
                    char a = line[i];
                    if (char.IsWhiteSpace(a)) { i++; continue; }
                    if (a is '"' or '\'')
                    {
                        i = ScanQuoted(line, i, a, TokenKind.String, tokens);
                        continue;
                    }
                    if (char.IsLetter(a) || a is '_' or ':')
                    {
                        int s = i;
                        while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '_' or ':' or '.' or '-')) i++;
                        Add(tokens, s, i - s, TokenKind.AttributeName);
                        continue;
                    }
                    if (a == '=') { Add(tokens, i, 1, TokenKind.Operator); i++; continue; }
                    i++;
                }

                if (i < line.Length && line[i] == '>') { Add(tokens, i, 1, TokenKind.XmlDelimiter); i++; }
                continue;
            }

            // 标签之间的文本
            int textStart = i;
            while (i < line.Length && line[i] != '<' && !Matches(line, i, "<!--")) i++;
            Add(tokens, textStart, i - textStart, TokenKind.XmlText);
        }

        return new LineTokens(tokens, LineState.None);
    }

    // ---------------------------------------------------------------- Markdown

    private static LineTokens TokenizeMarkdown(string line, LineState state)
    {
        var tokens = new List<Token>();

        if ((state & LineState.InMarkdownFence) != 0)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                return new LineTokens(new List<Token> { new(0, line.Length, TokenKind.Code) }, LineState.None);

            Add(tokens, 0, line.Length, TokenKind.Code);
            return new LineTokens(tokens, LineState.InMarkdownFence);
        }

        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            Add(tokens, 0, line.Length, TokenKind.Code);
            return new LineTokens(tokens, LineState.InMarkdownFence);
        }

        if (trimmed.StartsWith('#'))
        {
            int level = 0;
            while (level < line.Length && line[level] == '#') level++;
            Add(tokens, 0, line.Length, TokenKind.Heading);
            return new LineTokens(tokens, LineState.None);
        }

        if (trimmed is "---" or "***" or "___")
        {
            Add(tokens, 0, line.Length, TokenKind.HorizontalRule);
            return new LineTokens(tokens, LineState.None);
        }

        if (trimmed.StartsWith("> ", StringComparison.Ordinal) || trimmed == ">")
        {
            Add(tokens, 0, line.Length, TokenKind.Quote);
            return new LineTokens(tokens, LineState.None);
        }

        // 列表项
        bool isList = trimmed.StartsWith("- ", StringComparison.Ordinal)
                      || trimmed.StartsWith("* ", StringComparison.Ordinal)
                      || trimmed.StartsWith("+ ", StringComparison.Ordinal);
        int markerEnd = -1;
        if (!isList && trimmed.Length > 0 && char.IsDigit(trimmed[0]))
        {
            int dot = trimmed.IndexOf('.');
            if (dot > 0 && dot + 1 < trimmed.Length && trimmed[dot + 1] == ' ') { isList = true; markerEnd = dot + 2; }
        }
        if (isList)
        {
            int offset = line.Length - trimmed.Length;
            int len = markerEnd > 0 ? markerEnd : 2;
            Add(tokens, offset, len, TokenKind.ListMarker);
            ScanInline(line, offset + len, tokens);
            return new LineTokens(tokens, LineState.None);
        }

        ScanInline(line, 0, tokens);
        return new LineTokens(tokens, LineState.None);
    }

    private static void ScanInline(string line, int from, List<Token> tokens)
    {
        int i = from;
        while (i < line.Length)
        {
            char c = line[i];

            // 行内代码
            if (c == '`')
            {
                int end = line.IndexOf('`', i + 1);
                if (end > i) { Add(tokens, i, end - i + 1, TokenKind.Code); i = end + 1; continue; }
            }

            // 链接 [text](url)
            if (c == '[')
            {
                int close = line.IndexOf(']', i + 1);
                if (close > i && close + 1 < line.Length && line[close + 1] == '(')
                {
                    int end = line.IndexOf(')', close + 2);
                    if (end > close) { Add(tokens, i, end - i + 1, TokenKind.Link); i = end + 1; continue; }
                }
            }

            // 加粗
            if (Matches(line, i, "**"))
            {
                int end = line.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i) { Add(tokens, i, end - i + 2, TokenKind.Strong); i = end + 2; continue; }
            }

            // 斜体
            if (c is '*' or '_')
            {
                int end = line.IndexOf(c, i + 1);
                if (end > i + 1) { Add(tokens, i, end - i + 1, TokenKind.Emphasis); i = end + 1; continue; }
            }

            i++;
        }
    }

    // ---------------------------------------------------------------- 工具

    private static int ScanQuoted(string line, int start, char quote, TokenKind kind, List<Token> tokens)
    {
        int i = start + 1;
        while (i < line.Length)
        {
            if (line[i] == '\\') { i += 2; continue; }
            if (line[i] == quote) { i++; break; }
            i++;
        }
        Add(tokens, start, Math.Min(i, line.Length) - start, kind);
        return i;
    }

    private static int ScanNumber(string line, int start, List<Token> tokens)
    {
        int i = start;

        if (i + 1 < line.Length && line[i] == '0' && line[i + 1] is 'x' or 'X')
        {
            i += 2;
            while (i < line.Length && (char.IsAsciiHexDigit(line[i]) || line[i] == '_')) i++;
        }
        else
        {
            while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '_')) i++;
            if (i < line.Length && line[i] == '.')
            {
                i++;
                while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '_')) i++;
            }
            if (i < line.Length && line[i] is 'e' or 'E')
            {
                i++;
                if (i < line.Length && line[i] is '+' or '-') i++;
                while (i < line.Length && char.IsDigit(line[i])) i++;
            }
        }

        // 后缀：123L、1.5f、10u 等
        while (i < line.Length && char.IsAsciiLetter(line[i])) i++;

        Add(tokens, start, i - start, TokenKind.Number);
        return i;
    }

    private static void Add(List<Token> tokens, int start, int length, TokenKind kind)
    {
        if (length > 0) tokens.Add(new Token(start, length, kind));
    }

    private static bool Matches(string line, int index, string value) =>
        index + value.Length <= line.Length &&
        string.CompareOrdinal(line, index, value, 0, value.Length) == 0;

    private static bool IsFirstNonSpace(string line, int index)
    {
        for (int i = 0; i < index; i++)
        {
            if (!char.IsWhiteSpace(line[i])) return false;
        }
        return true;
    }

    private static bool IsOperator(char c) => c switch
    {
        '+' or '-' or '*' or '/' or '%' or '=' or '<' or '>' or '!' or '&' or '|' or '^' or '~' or '?' or ':' => true,
        _ => false,
    };
}
