// Copyright (c) 2026 ByteForge
using System.Windows.Documents;
using System.Windows.Media;
using ByteForge.Syntax;

namespace ByteForge.Editor.Wpf.Highlighting;

/// <summary>
/// 把词法扫描结果写进 FlowDocument。
/// 每个段落 = 一行，段落里的 Run 按记号上色。
/// </summary>
public sealed class SyntaxHighlightService
{
    /// <summary>超过此字节数不启用高亮（保内存）。</summary>
    public const long MaxHighlightBytes = 1024 * 1024;

    /// <summary>超过此行数不启用高亮。</summary>
    public const int MaxHighlightLines = 6000;

    private readonly Dictionary<TokenKind, SolidColorBrush> _brushCache = new();

    public SyntaxTheme Theme { get; private set; } = SyntaxTheme.Light;

    public void SetTheme(SyntaxTheme theme)
    {
        Theme = theme;
        _brushCache.Clear();
    }

    /// <summary>是否值得启用语法高亮。</summary>
    public static bool CanHighlight(LanguageDefinition language, long byteCount, int lineCount) =>
        language.Id != "plaintext" && byteCount <= MaxHighlightBytes && lineCount <= MaxHighlightLines;

    /// <summary>重排整个文档（同步，只在文档很小或必须一次性完成时用；一般走 CodeEditor 的分批着色）。</summary>
    public void HighlightDocument(FlowDocument document, LanguageDefinition language)
    {
        LineState state = LineState.None;

        // 必须先快照：HighlightParagraph 会重写段落里的 Inlines，
        // 而 document.Blocks 的枚举器只要文本容器一变就失效（InvalidOperationException）。
        foreach (Block block in document.Blocks.ToList())
        {
            if (block is not Paragraph paragraph) continue;
            state = HighlightParagraph(paragraph, language, state);
        }
    }

    /// <summary>从指定段落开始重排若干行（编辑时只重扫受影响的局部）。</summary>
    public void HighlightRange(FlowDocument document, LanguageDefinition language, int startIndex, int count)
    {
        var blocks = document.Blocks;
        if (startIndex < 0 || startIndex >= blocks.Count) return;

        // 状态从上一段的末尾往前推：简单起见，从 startIndex 之前 200 行开始重扫，保证块注释 / 三引号状态正确
        int scanFrom = Math.Max(0, startIndex - 200);
        LineState state = RecomputeState(document, language, scanFrom);

        for (int i = scanFrom; i < blocks.Count && i < startIndex + count; i++)
        {
            if (blocks.ElementAt(i) is Paragraph paragraph)
            {
                state = HighlightParagraph(paragraph, language, state);
            }
        }
    }

    private static LineState RecomputeState(FlowDocument document, LanguageDefinition language, int upTo)
    {
        LineState state = LineState.None;
        int index = 0;
        foreach (Block block in document.Blocks)
        {
            if (index >= upTo) break;
            if (block is Paragraph paragraph)
            {
                string line = ParagraphText(paragraph);
                state = Tokenizer.TokenizeLine(line, language, state).NextState;
            }
            index++;
        }
        return state;
    }

    /// <summary>给单个段落上色，返回该行结束时的扫描状态。</summary>
    public LineState HighlightParagraph(Paragraph paragraph, LanguageDefinition language, LineState state)
    {
        string line = ParagraphText(paragraph);
        var result = Tokenizer.TokenizeLine(line, language, state);

        paragraph.Inlines.Clear();
        int cursor = 0;

        foreach (Token token in result.Tokens.OrderBy(t => t.Start))
        {
            if (token.Start > cursor)
            {
                paragraph.Inlines.Add(MakeRun(line[cursor..token.Start], TokenKind.Plain));
            }

            int length = Math.Min(token.Length, line.Length - token.Start);
            if (length > 0)
            {
                paragraph.Inlines.Add(MakeRun(line.Substring(token.Start, length), token.Kind));
            }
            cursor = token.Start + token.Length;
        }

        if (cursor < line.Length)
        {
            paragraph.Inlines.Add(MakeRun(line[cursor..], TokenKind.Plain));
        }

        return result.NextState;
    }

    public static string ParagraphText(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    private Run MakeRun(string text, TokenKind kind) =>
        new(text) { Foreground = BrushFor(kind) };

    private SolidColorBrush BrushFor(TokenKind kind)
    {
        if (_brushCache.TryGetValue(kind, out var cached)) return cached;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Theme.Get(kind)));
        brush.Freeze();
        _brushCache[kind] = brush;
        return brush;
    }
}
