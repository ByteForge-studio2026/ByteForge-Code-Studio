// Copyright (c) 2026 ByteForge
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ByteForge.Core.Settings;
using ByteForge.Editor.Wpf.Highlighting;
using ByteForge.Syntax;

namespace ByteForge.Editor.Wpf.Controls;

/// <summary>一次搜索命中的信息：全文偏移、长度、所在行号、行内列号、所在行文本（用作前后文）。</summary>
public sealed record SearchResult(int Offset, int Length, int Line, int Column, string LineText);

/// <summary>
/// 代码编辑控件：行号栏 + RichTextBox。
/// - 一个 Paragraph 对应一行，便于按行重排；
/// - 打开时全文高亮，编辑时只重扫光标附近若干行（默认 40 行），不吃 CPU；
/// - 大文件 / 纯文本自动降级为"单段落纯文本"模式，保内存。
/// </summary>
public partial class CodeEditor : UserControl
{
    private static readonly Regex LineSplitter = new("\r\n|\r|\n", RegexOptions.Compiled);

    private readonly SyntaxHighlightService _highlighter = new();
    private readonly DispatcherTimer _highlightTimer;

    // 分批着色：打开 / 换主题时不再一次性把整篇扫完（几千行会把界面堵死几秒 → "未响应"），
    // 改成每批 HighlightChunkLines 行、批与批之间把控制权交回 UI 线程。
    private const int HighlightChunkLines = 120;
    private bool _progressiveRunning;
    private int _progressiveIndex;
    private LineState _progressiveState;

    private LanguageDefinition _language = LanguageRegistry.PlainText;
    private bool _suppressTextChanged;
    private bool _documentLoaded;
    private bool _isDirty;
    private bool _highlightEnabled;
    private bool _plainTextMode;
    private bool _tabToSpaces = true;
    private int _tabSize = 4;
    private bool _showLineNumbers = true;
    private bool _highlightCurrentLine = true;
    private bool _autoIndent = true;
    private bool _autoCloseBrackets = true;
    private long _maxHighlightBytes = 512 * 1024;
    private Paragraph? _currentLineParagraph;
    private Brush? _currentLineBrush;
    // 语法高亮正在改文档时，改段落背景会让高亮那边的枚举器失效，先挂起
    private bool _suspendCurrentLine;
    private int _caretLine;
    private int _caretColumn;
    private int _plainTextLineCount = 1;
    private ScrollViewer? _scrollViewer;

    // 搜索命中的高亮（黄底），点结果列表跳转时用
    // 记下 行/列/长度：分批着色跑到这一段时会把它刷掉，之后要能原样补回来
    private TextRange? _matchHighlight;
    private int _matchLine = -1;
    private int _matchColumn;
    private int _matchLength;
    private Brush _matchBrush = Brushes.Transparent;
    private Brush _matchTextBrush = Brushes.Black;

    public CodeEditor()
    {
        InitializeComponent();

        _highlightTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _highlightTimer.Tick += (_, _) =>
        {
            _highlightTimer.Stop();

            // 纯文本模式（大文件）：只刷新行号，不扫全文
            if (!_highlightEnabled)
            {
                _plainTextLineCount = CountLines(Text);
                UpdateGutter();
                return;
            }

            ReHighlightAroundCaret();
        };

        Editor.TextChanged += Editor_TextChanged;
        Editor.SelectionChanged += (_, _) =>
        {
            UpdateCaretInfo();
            CaretChanged?.Invoke(this, EventArgs.Empty);
        };
        Editor.PreviewKeyDown += Editor_PreviewKeyDown;
        Editor.PreviewTextInput += Editor_PreviewTextInput;
        Editor.SelectionChanged += (_, _) => ApplyCurrentLineHighlight();
        Loaded += (_, _) =>
        {
            AttachScrollViewer();
            UpdateGutter();
        };

        Editor.Document = NewDocument();
    }

    /// <summary>文本内容变化。</summary>
    public event EventHandler? TextChanged;

    /// <summary>光标位置变化。</summary>
    public event EventHandler? CaretChanged;

    /// <summary>语言标识，例如 python / csharp / markdown。</summary>
    public string LanguageId => _language.Id;

    /// <summary>语言显示名，例如 Python。</summary>
    public string LanguageDisplayName => _language.DisplayName;

    /// <summary>
    /// 是否已改动（保存后调 SetDirty(false)）。
    /// 只有真正通过 LoadText 载入过文档才算数——控件初始化 / 换字体触发的
    /// TextChanged 不会把"没打开过文件"的状态弄脏。
    /// </summary>
    public bool IsDirty => _documentLoaded && _isDirty;

    /// <summary>当前是否启用了语法高亮（大文件会自动关闭）。</summary>
    public bool HighlightEnabled => _highlightEnabled;

    public int CaretLine => _caretLine;
    public int CaretColumn => _caretColumn;

    /// <summary>
    /// 行数。按段落计数是 O(1)；纯文本模式（大文件）用缓存值，避免每次滚动都把全文取一遍。
    /// </summary>
    public int LineCount => _plainTextMode ? _plainTextLineCount : Editor.Document.Blocks.Count;

    /// <summary>文档纯文本。</summary>
    public string Text => new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;

    /// <summary>当前选中的文本。</summary>
    public string SelectedText => Editor.Selection.Text;

    /// <summary>选中文本长度（字符数，段落分隔按 \r\n 计）。</summary>
    public int SelectionLength => new TextRange(Editor.Selection.Start, Editor.Selection.End).Text.Length;

    /// <summary>选区起点在全文中的字符偏移（与 <see cref="Text"/> 同一坐标系）。</summary>
    public int SelectionStart =>
        new TextRange(Editor.Document.ContentStart, Editor.Selection.Start).Text.Length;

    // ------------------------------------------------------------ 查找

    /// <summary>统计全文里 term 出现的次数（matchCase=false 时不区分大小写）。</summary>
    public int CountMatches(string term, bool matchCase)
    {
        if (string.IsNullOrEmpty(term)) return 0;
        string text = Text;
        if (text.Length == 0) return 0;

        StringComparison c = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(term, idx, c)) >= 0)
        {
            count++;
            idx += term.Length;
        }
        return count;
    }

    /// <summary>统计在 offset 之前（不含）出现的匹配数，用于显示"第几个 / 共几个"。</summary>
    public int CountMatchesBefore(string term, bool matchCase, int offset)
    {
        if (string.IsNullOrEmpty(term)) return 0;
        string text = Text;
        if (text.Length == 0) return 0;

        StringComparison c = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(term, idx, c)) >= 0 && idx < offset)
        {
            count++;
            idx += term.Length;
        }
        return count;
    }

    /// <summary>
    /// 从当前选区继续查找下一个 / 上一个匹配，选中并滚入视野；找不到则绕回文档两端。
    /// 返回是否找到了匹配。
    /// </summary>
    public bool FindNext(string term, bool matchCase, bool forward)
    {
        if (string.IsNullOrEmpty(term)) return false;

        string text = Text;
        if (text.Length == 0) return false;

        StringComparison c = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int start = forward ? SelectionStart + SelectionLength : SelectionStart - 1;

        int found = forward
            ? text.IndexOf(term, Math.Max(0, start), c)
            : text.LastIndexOf(term, Math.Min(text.Length - 1, Math.Max(0, start)), c);

        if (found < 0)
        {
            // 绕回：正向回到开头，反向回到结尾
            found = forward
                ? text.IndexOf(term, 0, c)
                : text.LastIndexOf(term, text.Length - 1, c);
        }
        if (found < 0) return false;

        (int line, int col) = OffsetToLineColumn(text, found);
        SelectLineColumn(line, col, term.Length, center: false);
        return true;
    }

    /// <summary>从文档开头查找第一个匹配（查找条输入变化时用）。</summary>
    public bool FindFirst(string term, bool matchCase)
    {
        if (string.IsNullOrEmpty(term)) return false;

        IReadOnlyList<SearchResult> all = FindAll(term, matchCase);
        if (all.Count == 0) return false;

        SearchResult r = all[0];
        SelectLineColumn(r.Line - 1, r.Column, r.Length, center: false);
        return true;
    }

    /// <summary>列出全文所有匹配（含行号、行内列号、所在行文本，供结果面板展示）。</summary>
    public IReadOnlyList<SearchResult> FindAll(string term, bool matchCase)
    {
        var list = new List<SearchResult>();
        if (string.IsNullOrEmpty(term)) return list;

        string text = Text;
        if (text.Length == 0) return list;

        // 每行起始偏移（按 \n 切分，与 Text 的 \r\n 一致）
        var lineStarts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') lineStarts.Add(i + 1);
        }

        StringComparison c = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int idx = 0;
        while ((idx = text.IndexOf(term, idx, c)) >= 0)
        {
            int lineIndex = LineIndexAt(lineStarts, idx);
            int start = lineStarts[lineIndex];
            int end = lineIndex + 1 < lineStarts.Count ? lineStarts[lineIndex + 1] - 1 : text.Length;
            string lineText = text.Substring(start, Math.Max(0, end - start)).Replace("\r", string.Empty);
            list.Add(new SearchResult(idx, term.Length, lineIndex + 1, idx - start, lineText));
            idx += term.Length;
        }
        return list;
    }

    /// <summary>
    /// 跳到某个匹配（用 0 基行号 + 行内列号）：选中、滚进视野，并给命中片段加高亮背景。
    /// 用 行/列 而不是字符偏移，是因为纯文本模式整篇只有一个段落，段内换行是 LineBreak，
    /// 字符偏移和 TextPointer 的符号坐标对不上；按行定位在两种模式下都准确。
    /// </summary>
    public void GoToMatch(int lineIndex, int column, int length)
    {
        ClearMatchHighlight();

        TextPointer? from = PointerAtLineColumn(lineIndex, column);
        if (from is null) return;
        TextPointer to = from.GetPositionAtOffset(length, LogicalDirection.Forward) ?? from;

        // 滚到视口中间：跳过去之后能一眼看到，不会被底部查找面板挡住
        SelectRange(from, to, center: true);

        _matchLine = lineIndex;
        _matchColumn = column;
        _matchLength = length;
        ApplyMatchHighlight(from, to);
    }

    /// <summary>给命中片段刷黄底。from/to 为 null 时按记住的行列重新定位。</summary>
    private void ApplyMatchHighlight(TextPointer? from = null, TextPointer? to = null)
    {
        if (_matchLine < 0 || _matchLength <= 0) return;

        bool old = _suppressTextChanged;
        _suppressTextChanged = true;
        try
        {
            if (from is null || to is null)
            {
                from = PointerAtLineColumn(_matchLine, _matchColumn);
                if (from is null) return;
                to = from.GetPositionAtOffset(_matchLength, LogicalDirection.Forward) ?? from;
            }

            var range = new TextRange(from, to);
            range.ApplyPropertyValue(TextElement.BackgroundProperty, _matchBrush);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, _matchTextBrush);
            _matchHighlight = range;
        }
        catch
        {
            // 高亮加不上去也不影响跳转本身
        }
        finally
        {
            _suppressTextChanged = old;
        }
    }

    /// <summary>清掉上一次命中高亮（换结果 / 关查找 / 重新载入时调用）。</summary>
    public void ClearMatchHighlight()
    {
        var range = _matchHighlight;
        _matchHighlight = null;
        _matchLine = -1;
        _matchColumn = 0;
        _matchLength = 0;
        if (range is null) return;

        bool old = _suppressTextChanged;
        _suppressTextChanged = true;
        try
        {
            range.ApplyPropertyValue(TextElement.BackgroundProperty, null);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, null);
        }
        catch
        {
            // 清不掉就算了
        }
        finally
        {
            _suppressTextChanged = old;
        }
    }

    /// <summary>二分查找 offset 落在第几行（返回 0 基行号）。</summary>
    private static int LineIndexAt(List<int> lineStarts, int offset)
    {
        int lo = 0, hi = lineStarts.Count - 1, ans = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (lineStarts[mid] <= offset) { ans = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return ans;
    }

    // ------------------------------------------------------------ 载入 / 设置

    /// <summary>载入文本并按语言着色。</summary>
    public void LoadText(string text, string? languageId = null, string? filePath = null)
    {
        _language = languageId is null
            ? LanguageRegistry.GetByExtension(filePath)
            : LanguageRegistry.GetById(languageId);

        _highlightEnabled = SyntaxHighlightService.CanHighlight(
                                _language, text.Length * 2L, CountLines(text))
                            && WithinHighlightBudget(text);

        _suppressTextChanged = true;
        try
        {
            var document = NewDocument();
            _plainTextMode = !_highlightEnabled;

            if (_plainTextMode)
            {
                // 大文件 / 纯文本：整篇一个段落，不做着色
                document.Blocks.Add(NewParagraph(text));
            }
            else
            {
                foreach (string line in LineSplitter.Split(text))
                {
                    document.Blocks.Add(NewParagraph(line));
                }
            }

            ClearMatchHighlight();
            Editor.Document = document;

            if (_highlightEnabled)
            {
                // 首屏先同步画一批，剩下的分批在后台帧里接着画，界面不卡
                StartProgressiveHighlight();
            }

            _documentLoaded = true;
            _isDirty = false;
            _plainTextLineCount = CountLines(text);

            ApplyWrap();
            Editor.CaretPosition = Editor.Document.ContentStart;
            UpdateCaretInfo();
            UpdateGutter();
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    public void SetDirty(bool dirty) => _isDirty = dirty;

    /// <summary>应用设置（字体、换行、行号、Tab）。</summary>
    public void ApplySettings(AppSettings settings)
    {
        Editor.FontFamily = new FontFamily(settings.FontFamily);
        Editor.FontSize = settings.FontSize;
        Gutter.FontFamilyName = settings.FontFamily;
        Gutter.TextSize = Math.Max(9, settings.FontSize - 1);

        _tabToSpaces = settings.TabToSpaces;
        _tabSize = Math.Max(1, settings.TabSize);
        _showLineNumbers = settings.ShowLineNumbers;
        _highlightCurrentLine = settings.HighlightCurrentLine;
        _autoIndent = settings.AutoIndent;
        _autoCloseBrackets = settings.AutoCloseBrackets;
        _maxHighlightBytes = Math.Max(0, settings.HighlightMaxFileSizeKb) * 1024L;

        ApplyWrap(settings.WordWrap);
        UpdateGutter();
        ApplyCurrentLineHighlight();
    }

    /// <summary>应用主题（浅色 / 深色）。</summary>
    public void ApplyTheme(SyntaxTheme theme, bool dark)
    {
        _highlighter.SetTheme(theme);

        string background = dark ? "#1E1E1E" : "#FFFFFF";
        string foreground = dark ? "#E6E6E6" : "#1F1F1F";
        string gutterBackground = dark ? "#252526" : "#F5F5F5";
        string gutterForeground = dark ? "#6E7681" : "#8A8A8A";

        Root.Background = Solid(background);
        Editor.Foreground = Solid(foreground);
        Editor.CaretBrush = Solid(dark ? "#FFFFFF" : "#000000");
        Gutter.GutterBackground = Solid(gutterBackground);
        Gutter.NumberForeground = Solid(gutterForeground);

        _currentLineBrush = Solid(dark ? "#26262B" : "#F1F5FB");

        // 搜索命中的高亮：黄底 + 深色字，浅色深色下都看得清
        _matchBrush = Solid("#FFD166");
        _matchTextBrush = Solid("#1F1F1F");
        ApplyCurrentLineHighlight();

        if (_highlightEnabled)
        {
            // 换主题也走分批：几千行的文件一次性重排同样会把界面堵住
            StartProgressiveHighlight();
        }
    }

    // ------------------------------------------------------------ 分批着色

    /// <summary>开始（或重新开始）分批着色。</summary>
    private void StartProgressiveHighlight()
    {
        _progressiveRunning = true;
        _progressiveIndex = 0;
        _progressiveState = LineState.None;
        PumpHighlightChunk();
    }

    private void StopProgressiveHighlight() => _progressiveRunning = false;

    /// <summary>画一批（HighlightChunkLines 行），没画完就把下一批排到下一个后台帧。</summary>
    private void PumpHighlightChunk()
    {
        if (!_progressiveRunning) return;

        int count = Editor.Document.Blocks.Count;
        if (count == 0)
        {
            _progressiveRunning = false;
            return;
        }

        int end = Math.Min(count, _progressiveIndex + HighlightChunkLines);

        _suppressTextChanged = true;
        _suspendCurrentLine = true;
        Editor.BeginChange();
        try
        {
            for (int i = _progressiveIndex; i < end; i++)
            {
                if (Editor.Document.Blocks.ElementAt(i) is Paragraph paragraph)
                {
                    _progressiveState = _highlighter.HighlightParagraph(paragraph, _language, _progressiveState);
                }
            }
        }
        finally
        {
            Editor.EndChange();
            _suppressTextChanged = false;
            _suspendCurrentLine = false;
        }

        _progressiveIndex = end;

        if (_progressiveIndex < count)
        {
            // 交回 UI 线程：这一帧先去响应输入 / 重绘，下一帧再接着画
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(PumpHighlightChunk));
            return;
        }

        _progressiveRunning = false;

        // 着色会把命中片段所在的 Run 重建掉，黄底就丢了 —— 全画完后按 offset 补一次
        // （这些都是改格式不是改内容，必须压住 TextChanged，否则一打开的文件就成"已修改"）
        _suppressTextChanged = true;
        try
        {
            ApplyMatchHighlight();
            ApplyCurrentLineHighlight();
            UpdateGutter();
        }
        finally
        {
            _suppressTextChanged = false;
        }
    }

    /// <summary>切换自动换行（换行时行号栏隐藏，避免行号与视觉行错位）。</summary>
    public void ApplyWrap(bool? wrap = null)
    {
        bool enabled = wrap ?? (_wordWrap);
        _wordWrap = enabled;
        Editor.Document.PageWidth = enabled ? double.NaN : 1_000_000;
        Editor.HorizontalScrollBarVisibility = enabled ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        Gutter.Visibility = enabled || !_showLineNumbers ? Visibility.Collapsed : Visibility.Visible;
    }

    private bool _wordWrap;

    // ------------------------------------------------------------ 文档操作

    private static FlowDocument NewDocument() => new()
    {
        PageWidth = 1_000_000,
    };

    private static Paragraph NewParagraph(string text) => new(new Run(text))
    {
        Margin = new Thickness(0),
        Padding = new Thickness(0),
        TextIndent = 0,
    };

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged) return;

        // 用户已经开始编辑了：还没画完的批次作废，避免和光标附近的重排打架
        StopProgressiveHighlight();

        if (_documentLoaded) _isDirty = true;
        TextChanged?.Invoke(this, EventArgs.Empty);

        // 输入防抖：停手 150ms 再做重活
        _highlightTimer.Stop();
        _highlightTimer.Start();

        UpdateGutter();
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && _tabToSpaces)
        {
            string spaces = new(' ', _tabSize);
            Editor.Selection.Text = spaces;
            Editor.CaretPosition = Editor.Selection.End;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && _autoIndent)
        {
            string indent = LeadingWhitespaceOfCurrentLine();
            if (indent.Length == 0) return;

            // 先让回车把新段落建出来，再把缩进补进去
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    Editor.Selection.Text = indent;
                    Editor.CaretPosition = Editor.Selection.End;
                }
                catch
                {
                    // 缩进补不上也不能影响正常输入
                }
            }));
        }
    }

    /// <summary>输入括号 / 引号时自动补上另一半。</summary>
    private void Editor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_autoCloseBrackets || e.Text.Length != 1) return;

        string pair = e.Text[0] switch
        {
            '(' => "()",
            '[' => "[]",
            '{' => "{}",
            '"' => "\"\"",
            '\'' => "''",
            '`' => "``",
            _ => string.Empty,
        };

        if (pair.Length == 0) return;

        try
        {
            Editor.Selection.Text = pair;
            Editor.CaretPosition = Editor.CaretPosition.GetPositionAtOffset(-1, LogicalDirection.Forward)
                                   ?? Editor.CaretPosition;
            e.Handled = true;
        }
        catch
        {
            // 补不进去就按普通输入处理
        }
    }

    private string LeadingWhitespaceOfCurrentLine()
    {
        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph is null) return string.Empty;

        string line = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        int index = 0;
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) index++;
        return line[..index];
    }

    /// <summary>把光标所在行标出来。</summary>
    private void ApplyCurrentLineHighlight()
    {
        if (_suspendCurrentLine) return;

        bool old = _suppressTextChanged;
        _suppressTextChanged = true;
        try
        {
            var paragraph = Editor.CaretPosition.Paragraph;

            if (!ReferenceEquals(_currentLineParagraph, paragraph))
            {
                if (_currentLineParagraph is not null) _currentLineParagraph.Background = null;
                _currentLineParagraph = null;
            }

            if (!_highlightCurrentLine || paragraph is null) return;

            paragraph.Background = _currentLineBrush;
            _currentLineParagraph = paragraph;
        }
        catch
        {
            // 行高亮只是锦上添花，出任何问题都不能影响编辑
        }
        finally
        {
            _suppressTextChanged = old;
        }
    }

    /// <summary>跑语法高亮期间先挂起行高亮，避免两边同时改段落结构。</summary>
    private void WithCurrentLineSuspended(Action action)
    {
        _suspendCurrentLine = true;
        try { action(); }
        finally { _suspendCurrentLine = false; }

        ApplyCurrentLineHighlight();
    }

    /// <summary>超过设置的文件大小上限就自动关闭语法高亮（0 表示不限）。</summary>
    private bool WithinHighlightBudget(string text) =>
        _maxHighlightBytes <= 0 || text.Length * 2L <= _maxHighlightBytes;

    // ------------------------------------------------------------ 查找内部实现

    /// <summary>把全文字符偏移换算成 0 基行号 + 行内列号（按 \n 分行，与 Text 一致）。</summary>
    private static (int Line, int Column) OffsetToLineColumn(string text, int offset)
    {
        int line = 0, lineStart = 0;
        int limit = Math.Min(offset, text.Length);
        for (int i = 0; i < limit; i++)
        {
            if (text[i] == '\n') { line++; lineStart = i + 1; }
        }
        return (line, offset - lineStart);
    }

    /// <summary>
    /// 把"0 基行号 + 行内列号"映射成 TextPointer。
    /// 用 <see cref="TextPointer.GetLineStartPosition(int)"/> 定位行首，段落与段内换行都算行，
    /// 逐行段落（有语法高亮）和整篇一个段落（纯文本模式）两种模式都适用。
    /// </summary>
    private TextPointer? PointerAtLineColumn(int lineIndex, int column)
    {
        if (lineIndex < 0) lineIndex = 0;
        if (column < 0) column = 0;

        TextPointer baseStart = Editor.Document.ContentStart.GetLineStartPosition(0)
                                ?? Editor.Document.ContentStart;
        TextPointer? lineStart = lineIndex == 0 ? baseStart : baseStart.GetLineStartPosition(lineIndex);
        if (lineStart is null) return null;
        if (column == 0) return lineStart;

        return lineStart.GetPositionAtOffset(column, LogicalDirection.Forward) ?? lineStart;
    }

    /// <summary>选中「行/列 起、长 length」的一段并（可选）滚到视口中间。</summary>
    private void SelectLineColumn(int lineIndex, int column, int length, bool center)
    {
        TextPointer? from = PointerAtLineColumn(lineIndex, column);
        if (from is null) return;
        TextPointer to = from.GetPositionAtOffset(length, LogicalDirection.Forward) ?? from;
        SelectRange(from, to, center);
    }

    private void SelectRange(TextPointer start, TextPointer end, bool center = false)
    {
        Editor.Selection.Select(start, end);
        Editor.CaretPosition = end;
        // 不抢焦点：查找条里打字时焦点应留在输入框，编辑器选区滚动到可见即可
        ScrollToCaret(center);
    }

    /// <summary>
    /// 把光标（即选区末尾）滚进可见区域。
    /// center=true 时滚到视口中间 —— 点搜索结果跳转时用它，命中行不会被查找面板挡住。
    /// </summary>
    private void ScrollToCaret(bool center = false)
    {
        // 选区刚改完，布局可能还没更新，GetCharacterRect 会拿到旧坐标；
        // 推迟到下一帧布局完成后，按最新的矩形滚。
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_scrollViewer is null) return;

            try
            {
                Rect rect = Editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);
                double top = rect.Top;
                double bottom = rect.Bottom;
                double vo = _scrollViewer.VerticalOffset;
                double vh = _scrollViewer.ViewportHeight;
                const double margin = 6;

                if (center)
                {
                    double middle = top - (vh / 2) + (rect.Height / 2);
                    _scrollViewer.ScrollToVerticalOffset(Math.Max(0, middle));
                    return;
                }

                if (top < vo + margin)
                    _scrollViewer.ScrollToVerticalOffset(Math.Max(0, top - margin));
                else if (bottom > vo + vh - margin)
                    _scrollViewer.ScrollToVerticalOffset(Math.Max(0, bottom - vh + margin));
            }
            catch
            {
                // 滚动失败不影响查找本身
            }
        }), DispatcherPriority.Loaded);
    }

    private void ReHighlightAroundCaret()
    {
        if (!_highlightEnabled) return;

        var paragraph = Editor.CaretPosition.Paragraph;
        if (paragraph is null) return;

        int index = IndexOfBlock(Editor.Document.Blocks, paragraph);
        if (index < 0) return;

        int column = _caretColumn;

        _suppressTextChanged = true;
        _suspendCurrentLine = true;
        Editor.BeginChange();
        try
        {
            _highlighter.HighlightRange(Editor.Document, _language, index, 40);
        }
        finally
        {
            Editor.EndChange();
            _suppressTextChanged = false;
            _suspendCurrentLine = false;
        }

        RestoreCaret(index, column);
        UpdateGutter();
        ApplyCurrentLineHighlight();
    }

    private void RestoreCaret(int lineIndex, int column)
    {
        if (lineIndex >= Editor.Document.Blocks.Count) return;
        if (Editor.Document.Blocks.ElementAt(lineIndex) is not Paragraph paragraph) return;

        TextPointer? restored = PositionAt(paragraph, column);
        Editor.CaretPosition = restored ?? paragraph.ContentStart;
        UpdateCaretInfo();
    }

    private static TextPointer? PositionAt(Paragraph paragraph, int column)
    {
        int remaining = Math.Max(0, column);
        foreach (Inline inline in paragraph.Inlines)
        {
            if (inline is not Run run) continue;
            if (remaining <= run.Text.Length)
            {
                return run.ContentStart.GetPositionAtOffset(remaining, LogicalDirection.Forward);
            }
            remaining -= run.Text.Length;
        }
        return paragraph.ContentEnd;
    }

    private void UpdateCaretInfo()
    {
        var caret = Editor.CaretPosition;
        var paragraph = caret.Paragraph;
        if (paragraph is null) return;

        // 段落数先算上：纯文本模式整篇只有一个段落，行号得靠段落内的换行数补
        int line = 0;
        foreach (Block block in Editor.Document.Blocks)
        {
            if (ReferenceEquals(block, paragraph)) break;
            if (block is Paragraph) line++;
        }

        // 段落起点到光标之间的文本：数换行得段内行号，最后一个换行之后是列号
        string before = new TextRange(paragraph.ContentStart, caret).Text;
        int col = 0;
        foreach (char c in before)
        {
            if (c == '\n') { line++; col = 0; }
            else if (c != '\r') col++;
        }

        _caretLine = line;
        _caretColumn = col;
    }

    // ------------------------------------------------------------ 行号栏

    private void AttachScrollViewer()
    {
        _scrollViewer ??= FindVisualChild<ScrollViewer>(Editor);
        if (_scrollViewer is null) return;

        _scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
        _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        SyncGutter();
    }

    private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) => SyncGutter();

    private void UpdateGutter()
    {
        Gutter.LineCount = Math.Max(1, LineCount);
        Gutter.Visibility = _wordWrap || !_showLineNumbers ? Visibility.Collapsed : Visibility.Visible;
        Gutter.InvalidateMeasure();
        SyncGutter();
    }

    private void SyncGutter()
    {
        if (_scrollViewer is null) return;

        double lineHeight = Editor.FontFamily.LineSpacing * Editor.FontSize;
        Gutter.LineHeight = lineHeight;
        Gutter.LineCount = Math.Max(1, LineCount);
        Gutter.FirstVisibleLine = lineHeight > 0
            ? (int)Math.Floor(_scrollViewer.VerticalOffset / lineHeight)
            : 0;
        Gutter.TopOffset = lineHeight > 0 ? -(_scrollViewer.VerticalOffset % lineHeight) : 0;
    }

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>BlockCollection 没有 IndexOf，自己数一遍（只在光标移动时调用，代价可忽略）。</summary>
    private static int IndexOfBlock(BlockCollection blocks, Block block)
    {
        int index = 0;
        foreach (Block current in blocks)
        {
            if (ReferenceEquals(current, block)) return index;
            index++;
        }
        return -1;
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        int count = 1;
        foreach (char c in text)
        {
            if (c == '\n') count++;
        }
        return count;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;

            var found = FindVisualChild<T>(child);
            if (found is not null) return found;
        }
        return null;
    }
}
