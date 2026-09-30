// Copyright (c) 2026 ByteForge
namespace ByteForge.Syntax;

/// <summary>记号类型。</summary>
public enum TokenKind
{
    Plain,
    Comment,
    String,
    Char,
    Number,
    Keyword,
    Type,
    Identifier,
    Operator,
    Punctuation,
    Preprocessor,

    // 标记语言（XML / XAML）
    Tag,
    AttributeName,
    XmlText,
    XmlDelimiter,

    // Markdown
    Heading,
    Strong,
    Emphasis,
    Code,
    Link,
    ListMarker,
    Quote,
    HorizontalRule,
}

/// <summary>一行内的记号（Start / Length 均相对行首）。</summary>
public readonly record struct Token(int Start, int Length, TokenKind Kind);

/// <summary>跨行状态位。</summary>
[Flags]
public enum LineState
{
    None = 0,
    InBlockComment = 1,
    InTripleString = 2,
    InMarkdownFence = 4,
}

/// <summary>一行的扫描结果。</summary>
public readonly record struct LineTokens(List<Token> Tokens, LineState NextState);
