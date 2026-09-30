// Copyright (c) 2026 ByteForge
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ByteForge.Editor.Wpf.Controls;

/// <summary>
/// 行号栏。用 DrawingContext 直接画，只为"可见的那几行"生成文本，
/// 不建控件、不建 Run，内存开销跟行数无关。
/// </summary>
public sealed class LineNumberMargin : FrameworkElement
{
    public static readonly DependencyProperty LineCountProperty = DependencyProperty.Register(
        nameof(LineCount), typeof(int), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FirstVisibleLineProperty = DependencyProperty.Register(
        nameof(FirstVisibleLine), typeof(int), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TopOffsetProperty = DependencyProperty.Register(
        nameof(TopOffset), typeof(double), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NumberForegroundProperty = DependencyProperty.Register(
        nameof(NumberForeground), typeof(Brush), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GutterBackgroundProperty = DependencyProperty.Register(
        nameof(GutterBackground), typeof(Brush), typeof(LineNumberMargin),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public int LineCount { get => (int)GetValue(LineCountProperty); set => SetValue(LineCountProperty, value); }
    public int FirstVisibleLine { get => (int)GetValue(FirstVisibleLineProperty); set => SetValue(FirstVisibleLineProperty, value); }
    public double LineHeight { get => (double)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double TopOffset { get => (double)GetValue(TopOffsetProperty); set => SetValue(TopOffsetProperty, value); }
    public Brush NumberForeground { get => (Brush)GetValue(NumberForegroundProperty); set => SetValue(NumberForegroundProperty, value); }
    public Brush GutterBackground { get => (Brush)GetValue(GutterBackgroundProperty); set => SetValue(GutterBackgroundProperty, value); }

    public string FontFamilyName { get; set; } = "Consolas";
    public double TextSize { get; set; } = 12;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        dc.DrawRectangle(GutterBackground, null, new Rect(0, 0, width, height));

        var typeface = new Typeface(new FontFamily(FontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        double lineHeight = LineHeight;
        if (lineHeight <= 0) return;

        double y = TopOffset;
        int line = FirstVisibleLine;

        while (y < height && line < LineCount)
        {
            string text = (line + 1).ToString(CultureInfo.InvariantCulture);
            var formatted = new FormattedText(
                text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, TextSize,
                NumberForeground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formatted, new Point(width - formatted.Width - 6, y));
            y += lineHeight;
            line++;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int digits = Math.Max(2, LineCount.ToString(CultureInfo.InvariantCulture).Length);
        double width = digits * TextSize * 0.62 + 14;
        return new Size(width, availableSize.Height);
    }
}
