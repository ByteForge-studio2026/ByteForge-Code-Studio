// Copyright (c) 2026 ByteForge
namespace ByteForge.Markdown;

/// <summary>Markdown 文档模型（与 UI 无关）。</summary>
public abstract record MarkdownBlock;

public sealed record HeadingBlock(int Level, List<MarkdownInline> Content) : MarkdownBlock;

public sealed record ParagraphBlock(List<MarkdownInline> Content, bool IsTableRow = false) : MarkdownBlock;

public sealed record CodeBlock(string? Language, string Code) : MarkdownBlock;

public sealed record QuoteBlock(List<MarkdownBlock> Children) : MarkdownBlock;

public sealed record ListBlock(bool Ordered, List<ListItemBlock> Items) : MarkdownBlock;

public sealed record ListItemBlock(List<MarkdownInline> Content, int IndentLevel) : MarkdownBlock;

public sealed record HorizontalRuleBlock : MarkdownBlock;

public sealed record TableBlock(List<string[]> Rows, bool[] AlignRight) : MarkdownBlock;

/// <summary>行内元素。</summary>
public abstract record MarkdownInline;

public sealed record TextInline(string Text) : MarkdownInline;

public sealed record CodeInline(string Code) : MarkdownInline;

public sealed record StrongInline(List<MarkdownInline> Children) : MarkdownInline;

public sealed record EmphasisInline(List<MarkdownInline> Children) : MarkdownInline;

public sealed record LinkInline(string Text, string Url) : MarkdownInline;

public sealed record LineBreakInline : MarkdownInline;

/// <summary>解析结果。</summary>
public sealed record MarkdownDocument(List<MarkdownBlock> Blocks);
