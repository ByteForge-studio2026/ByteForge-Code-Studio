// Copyright (c) 2026 ByteForge
namespace ByteForge.Syntax;

/// <summary>语言定义：描述如何扫描一种语言的文本。</summary>
public sealed class LanguageDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string[] Extensions { get; init; } = Array.Empty<string>();

    public HashSet<string> Keywords { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> Types { get; init; } = new(StringComparer.Ordinal);

    /// <summary>行注释前缀，例如 "//" 或 "#"。</summary>
    public string? LineComment { get; init; }

    public string? BlockCommentStart { get; init; }
    public string? BlockCommentEnd { get; init; }

    /// <summary>字符串引号。</summary>
    public char[] QuoteChars { get; init; } = { '"' };

    /// <summary>是否支持字符字面量（C / C++ / Java / C#）。</summary>
    public bool SupportsCharLiteral { get; init; }

    /// <summary>是否支持预处理指令（#include / #define / #region）。</summary>
    public bool SupportsPreprocessor { get; init; }

    /// <summary>三引号字符串（Python 的 """docstring"""）。</summary>
    public string? TripleQuote { get; init; }

    /// <summary>标记语言（XML / XAML）走标签扫描。</summary>
    public bool IsMarkup { get; init; }

    /// <summary>Markdown 走 Markdown 扫描。</summary>
    public bool IsMarkdown { get; init; }

    /// <summary>标识符可用字符（默认下划线 + 字母数字）。</summary>
    public HashSet<char> ExtraIdentifierChars { get; init; } = new();

    public bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_' || c == '@' || ExtraIdentifierChars.Contains(c);

    public bool IsIdentifierPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '@' || ExtraIdentifierChars.Contains(c);
}
