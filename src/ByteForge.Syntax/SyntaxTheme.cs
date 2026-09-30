// Copyright (c) 2026 ByteForge
namespace ByteForge.Syntax;

/// <summary>
/// 语法配色。用十六进制字符串表达，不引用 WPF 的 Color 类型，
/// 保证语法模块可以被 WinUI / WPF / 控制台 / 单元测试共用。
/// </summary>
public sealed class SyntaxTheme
{
    public required string Name { get; init; }
    public required IReadOnlyDictionary<TokenKind, string> Colors { get; init; }

    public static SyntaxTheme Light { get; } = new()
    {
        Name = "浅色",
        Colors = new Dictionary<TokenKind, string>
        {
            [TokenKind.Plain] = "#1F1F1F",
            [TokenKind.Comment] = "#2E7D32",
            [TokenKind.String] = "#A31515",
            [TokenKind.Char] = "#A31515",
            [TokenKind.Number] = "#098658",
            [TokenKind.Keyword] = "#0000FF",
            [TokenKind.Type] = "#267F99",
            [TokenKind.Identifier] = "#1F1F1F",
            [TokenKind.Operator] = "#1F1F1F",
            [TokenKind.Punctuation] = "#575757",
            [TokenKind.Preprocessor] = "#9C27B0",
            [TokenKind.Tag] = "#A31515",
            [TokenKind.AttributeName] = "#E50000",
            [TokenKind.XmlText] = "#1F1F1F",
            [TokenKind.XmlDelimiter] = "#0000FF",
            [TokenKind.Heading] = "#1565C0",
            [TokenKind.Strong] = "#1F1F1F",
            [TokenKind.Emphasis] = "#1F1F1F",
            [TokenKind.Code] = "#A31515",
            [TokenKind.Link] = "#1565C0",
            [TokenKind.ListMarker] = "#098658",
            [TokenKind.Quote] = "#575757",
            [TokenKind.HorizontalRule] = "#B0B0B0",
        },
    };

    public static SyntaxTheme Dark { get; } = new()
    {
        Name = "深色",
        Colors = new Dictionary<TokenKind, string>
        {
            [TokenKind.Plain] = "#E6E6E6",
            [TokenKind.Comment] = "#6A9955",
            [TokenKind.String] = "#CE9178",
            [TokenKind.Char] = "#CE9178",
            [TokenKind.Number] = "#B5CEA8",
            [TokenKind.Keyword] = "#569CD6",
            [TokenKind.Type] = "#4EC9B0",
            [TokenKind.Identifier] = "#E6E6E6",
            [TokenKind.Operator] = "#D4D4D4",
            [TokenKind.Punctuation] = "#A0A0A0",
            [TokenKind.Preprocessor] = "#C586C0",
            [TokenKind.Tag] = "#569CD6",
            [TokenKind.AttributeName] = "#9CDCFE",
            [TokenKind.XmlText] = "#E6E6E6",
            [TokenKind.XmlDelimiter] = "#808080",
            [TokenKind.Heading] = "#6CB6FF",
            [TokenKind.Strong] = "#FFFFFF",
            [TokenKind.Emphasis] = "#E6E6E6",
            [TokenKind.Code] = "#CE9178",
            [TokenKind.Link] = "#6CB6FF",
            [TokenKind.ListMarker] = "#B5CEA8",
            [TokenKind.Quote] = "#A0A0A0",
            [TokenKind.HorizontalRule] = "#505050",
        },
    };

    public static SyntaxTheme FromName(string? name) =>
        string.Equals(name, "dark", StringComparison.OrdinalIgnoreCase) ? Dark : Light;

    public string Get(TokenKind kind) =>
        Colors.TryGetValue(kind, out var color) ? color : Colors[TokenKind.Plain];
}
