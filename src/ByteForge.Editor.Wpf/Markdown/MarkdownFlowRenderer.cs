// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ByteForge.Markdown;

namespace ByteForge.Editor.Wpf.Preview;

/// <summary>把 Markdown 文档模型渲染成 FlowDocument（预览用）。</summary>
public static class MarkdownFlowRenderer
{
    public static FlowDocument Render(MarkdownDocument document, bool dark)
    {
        var flow = new FlowDocument
        {
            PageWidth = double.NaN,
            FontFamily = new FontFamily("Microsoft YaHei UI"),
            FontSize = 15,
            Foreground = Solid(dark ? "#E6E6E6" : "#1F1F1F"),
        };

        foreach (MarkdownBlock block in document.Blocks)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    flow.Blocks.Add(RenderHeading(heading));
                    break;
                case CodeBlock code:
                    flow.Blocks.Add(RenderCode(code, dark));
                    break;
                case QuoteBlock quote:
                    RenderQuote(flow, quote, dark);
                    break;
                case ListBlock list:
                    flow.Blocks.Add(RenderList(list));
                    break;
                case TableBlock table:
                    flow.Blocks.Add(RenderTable(table));
                    break;
                case HorizontalRuleBlock:
                    flow.Blocks.Add(new Paragraph(new Run("─".PadRight(60, '─')))
                    {
                        Foreground = Solid(dark ? "#505050" : "#D0D0D0"),
                        Margin = new Thickness(0, 8, 0, 8),
                    });
                    break;
                case ParagraphBlock paragraph:
                    flow.Blocks.Add(RenderParagraph(paragraph));
                    break;
                case ListItemBlock item:
                    flow.Blocks.Add(RenderListItem("• ", item));
                    break;
            }
        }

        return flow;
    }

    private static Paragraph RenderHeading(HeadingBlock heading)
    {
        double[] sizes = { 26, 22, 19, 17, 15, 14 };
        var paragraph = new Paragraph
        {
            FontSize = heading.Level <= sizes.Length ? sizes[heading.Level - 1] : 14,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 14, 0, 6),
            Foreground = Solid("#1565C0"),
        };
        paragraph.Inlines.AddRange(RenderInlines(heading.Content).Select(i => (Inline)i));
        return paragraph;
    }

    private static BlockUIContainer RenderCode(CodeBlock code, bool dark)
    {
        var text = new TextBlock
        {
            Text = code.Code,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Solid(dark ? "#CE9178" : "#A31515"),
        };

        var border = new Border
        {
            Child = text,
            Background = Solid(dark ? "#252526" : "#F5F5F5"),
            BorderBrush = Solid(dark ? "#3A3A3A" : "#E0E0E0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 6, 0, 6),
        };

        return new BlockUIContainer(border);
    }

    private static void RenderQuote(FlowDocument flow, QuoteBlock quote, bool dark)
    {
        var border = new Border
        {
            BorderBrush = Solid(dark ? "#3A3A3A" : "#C7C7C7"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 2, 2),
            Margin = new Thickness(0, 6, 0, 6),
        };

        var inner = new StackPanel();
        foreach (MarkdownBlock child in quote.Children)
        {
            var preview = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Solid(dark ? "#A0A0A0" : "#575757"),
                Margin = new Thickness(0, 2, 0, 2),
            };
            preview.Inlines.AddRange(ToWpfInlines(Flatten(child)));
            inner.Children.Add(preview);
        }
        border.Child = inner;
        flow.Blocks.Add(new BlockUIContainer(border));
    }

    private static List<MarkdownInline> Flatten(MarkdownBlock block) => block switch
    {
        ParagraphBlock p => p.Content,
        HeadingBlock h => h.Content,
        ListItemBlock i => i.Content,
        _ => new List<MarkdownInline> { new TextInline(string.Empty) },
    };

    private static Block RenderList(ListBlock list)
    {
        var section = new Section { Margin = new Thickness(0, 4, 0, 4) };
        for (int i = 0; i < list.Items.Count; i++)
        {
            string marker = list.Ordered ? $"{i + 1}. " : "• ";
            section.Blocks.Add(RenderListItem(marker, list.Items[i]));
        }
        return section;
    }

    private static Paragraph RenderListItem(string marker, ListItemBlock item)
    {
        var paragraph = new Paragraph { Margin = new Thickness(18, 1, 0, 1) };
        paragraph.Inlines.Add(new Run(marker) { Foreground = Solid("#098658"), FontWeight = FontWeights.Bold });
        paragraph.Inlines.AddRange(RenderInlines(item.Content).Select(i => (Inline)i));
        return paragraph;
    }

    private static Table RenderTable(TableBlock table)
    {
        var wpfTable = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };
        int columns = table.Rows.Count > 0 ? table.Rows[0].Length : 0;
        if (columns == 0) return wpfTable;

        for (int i = 0; i < columns; i++)
        {
            wpfTable.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var row = new TableRow();
            bool isHeader = r == 0 && table.Rows.Count > 1;
            for (int c = 0; c < columns; c++)
            {
                string text = c < table.Rows[r].Length ? table.Rows[r][c] : string.Empty;
                var cell = new TableCell(new Paragraph(new Run(text))
                {
                    FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                    Margin = new Thickness(4, 2, 4, 2),
                })
                {
                    BorderBrush = Solid("#D0D0D0"),
                    BorderThickness = new Thickness(1),
                };
                row.Cells.Add(cell);
            }
            group.Rows.Add(row);
        }

        wpfTable.RowGroups.Add(group);
        return wpfTable;
    }

    private static Paragraph RenderParagraph(ParagraphBlock paragraph)
    {
        var result = new Paragraph { Margin = new Thickness(0, 4, 0, 8), TextAlignment = TextAlignment.Left };
        result.Inlines.AddRange(RenderInlines(paragraph.Content).Select(i => (Inline)i));
        return result;
    }

    private static List<System.Windows.Documents.Inline> RenderInlines(List<MarkdownInline> inlines)
    {
        var result = new List<System.Windows.Documents.Inline>();
        foreach (MarkdownInline inline in inlines)
        {
            switch (inline)
            {
                case TextInline text:
                    result.Add(new Run(text.Text));
                    break;
                case CodeInline code:
                    result.Add(new Run(code.Code)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = Solid("#F0F0F0"),
                        Foreground = Solid("#A31515"),
                    });
                    break;
                case StrongInline strong:
                    var bold = new Bold();
                    bold.Inlines.AddRange(RenderInlines(strong.Children));
                    result.Add(bold);
                    break;
                case EmphasisInline emphasis:
                    var italic = new Italic();
                    italic.Inlines.AddRange(RenderInlines(emphasis.Children));
                    result.Add(italic);
                    break;
                case LinkInline link:
                    var hyperlink = new Hyperlink(new Run(link.Text))
                    {
                        NavigateUri = Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ? uri : null,
                        Foreground = Solid("#1565C0"),
                    };
                    hyperlink.RequestNavigate += (_, e) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
                        }
                        catch
                        {
                            // 打不开就算了，不打断编辑
                        }
                        e.Handled = true;
                    };
                    result.Add(hyperlink);
                    break;
                case LineBreakInline:
                    result.Add(new LineBreak());
                    break;
            }
        }
        return result;
    }

    private static List<System.Windows.Documents.Inline> ToWpfInlines(List<MarkdownInline> inlines) =>
        RenderInlines(inlines);

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
