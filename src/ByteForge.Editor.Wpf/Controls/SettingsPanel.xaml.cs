// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ByteForge.Core;
using ByteForge.Core.Settings;

namespace ByteForge.Editor.Wpf.Controls;

/// <summary>
/// 应用内设置面板：直接读写 AppSettings，保存后通知宿主应用新设置。
/// 之前这些设置放在独立的 WinUI 设置程序里，现在合并进编辑器，只留一个 exe。
/// </summary>
public partial class SettingsPanel : UserControl
{
    private readonly AppSettings _settings = SettingsStore.Current;

    /// <summary>设置已保存（宿主应据此重新应用主题 / 编辑器设置）。</summary>
    public event EventHandler? Saved;

    /// <summary>点关闭。</summary>
    public event EventHandler? Closed;

    public SettingsPanel()
    {
        InitializeComponent();
        Load();
        FillAbout();
        NavList.SelectedIndex = 0;
    }

    // ------------------------------------------------------------ 载入 / 保存

    private void Load()
    {
        SelectTag(CmbTheme, _settings.Theme);
        CmbFont.Text = _settings.FontFamily;
        SliderFont.Value = _settings.FontSize;
        TxtFontSize.Text = _settings.FontSize.ToString("0");

        ChkLineNumbers.IsChecked = _settings.ShowLineNumbers;
        ChkWordWrap.IsChecked = _settings.WordWrap;
        ChkCurrentLine.IsChecked = _settings.HighlightCurrentLine;
        ChkAutoIndent.IsChecked = _settings.AutoIndent;
        ChkAutoClose.IsChecked = _settings.AutoCloseBrackets;
        ChkStatusBar.IsChecked = _settings.ShowStatusBar;

        SliderTab.Value = Math.Max(1, _settings.TabSize);
        TxtTabSize.Text = _settings.TabSize.ToString();
        ChkTabToSpaces.IsChecked = _settings.TabToSpaces;
        SelectTag(CmbLineEnding, _settings.LineEnding);
        ChkTrimTrailing.IsChecked = _settings.TrimTrailingWhitespaceOnSave;
        ChkFinalNewline.IsChecked = _settings.InsertFinalNewlineOnSave;

        SelectTag(CmbEncoding, _settings.EncodingPreference);
        SelectTag(CmbPreview, _settings.MarkdownPreviewMode);
        SliderHighlight.Value = _settings.HighlightMaxFileSizeKb;
        TxtHighlight.Text = _settings.HighlightMaxFileSizeKb == 0
            ? "不限"
            : _settings.HighlightMaxFileSizeKb.ToString();

        ChkOpenLast.IsChecked = _settings.OpenLastFileOnStart;
        ChkRememberSize.IsChecked = _settings.RememberWindowSize;
        SliderRecent.Value = _settings.MaxRecentFiles;
        TxtRecent.Text = _settings.MaxRecentFiles.ToString();

        ChkDefaultEditor.IsChecked = _settings.IsDefaultEditor;
        TxtExtensions.Text = string.Join(", ", _settings.AssociatedExtensions);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Theme = TagOf(CmbTheme, "light");
        _settings.FontFamily = string.IsNullOrWhiteSpace(CmbFont.Text) ? "Consolas" : CmbFont.Text.Trim();
        _settings.FontSize = SliderFont.Value;

        _settings.ShowLineNumbers = ChkLineNumbers.IsChecked == true;
        _settings.WordWrap = ChkWordWrap.IsChecked == true;
        _settings.HighlightCurrentLine = ChkCurrentLine.IsChecked == true;
        _settings.AutoIndent = ChkAutoIndent.IsChecked == true;
        _settings.AutoCloseBrackets = ChkAutoClose.IsChecked == true;
        _settings.ShowStatusBar = ChkStatusBar.IsChecked == true;

        _settings.TabSize = (int)SliderTab.Value;
        _settings.TabToSpaces = ChkTabToSpaces.IsChecked == true;
        _settings.LineEnding = TagOf(CmbLineEnding, "crlf");
        _settings.TrimTrailingWhitespaceOnSave = ChkTrimTrailing.IsChecked == true;
        _settings.InsertFinalNewlineOnSave = ChkFinalNewline.IsChecked == true;

        _settings.EncodingPreference = TagOf(CmbEncoding, "utf8");
        _settings.MarkdownPreviewMode = TagOf(CmbPreview, "bottom");
        _settings.HighlightMaxFileSizeKb = (int)SliderHighlight.Value;

        _settings.OpenLastFileOnStart = ChkOpenLast.IsChecked == true;
        _settings.RememberWindowSize = ChkRememberSize.IsChecked == true;
        _settings.MaxRecentFiles = (int)SliderRecent.Value;

        _settings.IsDefaultEditor = ChkDefaultEditor.IsChecked == true;
        _settings.AssociatedExtensions = ParseExtensions(TxtExtensions.Text);

        SettingsStore.Save(_settings);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppSettings();
        defaults.RecentFiles = new List<string>(_settings.RecentFiles);
        defaults.LastOpenFile = _settings.LastOpenFile;

        _settings.Theme = defaults.Theme;
        _settings.FontFamily = defaults.FontFamily;
        _settings.FontSize = defaults.FontSize;
        _settings.ShowLineNumbers = defaults.ShowLineNumbers;
        _settings.WordWrap = defaults.WordWrap;
        _settings.HighlightCurrentLine = defaults.HighlightCurrentLine;
        _settings.AutoIndent = defaults.AutoIndent;
        _settings.AutoCloseBrackets = defaults.AutoCloseBrackets;
        _settings.ShowStatusBar = defaults.ShowStatusBar;
        _settings.TabSize = defaults.TabSize;
        _settings.TabToSpaces = defaults.TabToSpaces;
        _settings.LineEnding = defaults.LineEnding;
        _settings.TrimTrailingWhitespaceOnSave = defaults.TrimTrailingWhitespaceOnSave;
        _settings.InsertFinalNewlineOnSave = defaults.InsertFinalNewlineOnSave;
        _settings.EncodingPreference = defaults.EncodingPreference;
        _settings.MarkdownPreviewMode = defaults.MarkdownPreviewMode;
        _settings.HighlightMaxFileSizeKb = defaults.HighlightMaxFileSizeKb;
        _settings.OpenLastFileOnStart = defaults.OpenLastFileOnStart;
        _settings.RememberWindowSize = defaults.RememberWindowSize;
        _settings.MaxRecentFiles = defaults.MaxRecentFiles;
        _settings.IsDefaultEditor = defaults.IsDefaultEditor;
        _settings.AssociatedExtensions = new List<string>(AppSettings.DefaultExtensions());

        SettingsStore.Save(_settings);
        Load();
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    /// <summary>按浅色 / 深色给面板内的文字与输入控件上色。</summary>
    public void ApplyTheme(bool dark)
    {
        string fg = dark ? "#E6E6E6" : "#1F1F1F";
        string field = dark ? "#2D2D2D" : "#FFFFFF";
        string border = dark ? "#3A3A3A" : "#D0D0D4";

        Foreground = Solid(fg);

        foreach (var box in new[] { CmbTheme, CmbFont, CmbLineEnding, CmbEncoding, CmbPreview })
        {
            box.Background = Solid(field);
            box.Foreground = Solid(fg);
            box.BorderBrush = Solid(border);
        }

        TxtExtensions.Background = Solid(field);
        TxtExtensions.Foreground = Solid(fg);
        TxtExtensions.BorderBrush = Solid(border);
    }

    private static System.Windows.Media.SolidColorBrush Solid(string hex)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ------------------------------------------------------------ 分类切换 / 滑块

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        string tag = (NavList.SelectedItem as ListBoxItem)?.Tag as string ?? "appearance";

        AppearancePanel.Visibility = tag == "appearance" ? Visibility.Visible : Visibility.Collapsed;
        EditorPanel.Visibility = tag == "editor" ? Visibility.Visible : Visibility.Collapsed;
        IndentPanel.Visibility = tag == "indent" ? Visibility.Visible : Visibility.Collapsed;
        SavePanel.Visibility = tag == "save" ? Visibility.Visible : Visibility.Collapsed;
        StartupPanel.Visibility = tag == "startup" ? Visibility.Visible : Visibility.Collapsed;
        AssocPanel.Visibility = tag == "assoc" ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = tag == "about" ? Visibility.Visible : Visibility.Collapsed;
    }

    // 注意：XAML 里给 Slider 设 Minimum / Maximum 会在元素树还没建完时就触发一次
    // ValueChanged（RangeBase 强制 Value 落进范围），此时对应的 TextBlock 还是 null。
    // 所以每个处理函数都必须先判空，否则构造函数里就会 NullReferenceException，整个程序打不开。

    private void SliderFont_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtFontSize != null) TxtFontSize.Text = SliderFont.Value.ToString("0");
    }

    private void SliderTab_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtTabSize != null) TxtTabSize.Text = ((int)SliderTab.Value).ToString();
    }

    private void SliderHighlight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtHighlight != null)
            TxtHighlight.Text = SliderHighlight.Value <= 0 ? "不限" : ((int)SliderHighlight.Value).ToString();
    }

    private void SliderRecent_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtRecent != null) TxtRecent.Text = ((int)SliderRecent.Value).ToString();
    }

    private void RestoreExt_Click(object sender, RoutedEventArgs e) =>
        TxtExtensions.Text = string.Join(", ", AppSettings.DefaultExtensions());

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.GetDirectoryName(AppPaths.SettingsFilePath) ?? AppPaths.DataDirectory;
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        }
        catch
        {
            // 打不开就算了
        }
    }

    // ------------------------------------------------------------ 辅助

    private void FillAbout()
    {
        AboutName.Text = AppInfo.Name;
        AboutVersion.Text = AppInfo.Version;
        AboutPositioning.Text = AppInfo.Positioning;
        AboutTech.Text = AppInfo.TechStack;
        AboutConfigPath.Text = AppPaths.SettingsFilePath;
        AboutCopyright.Text = $"{AppInfo.Copyright} · {AppInfo.License}";
    }

    private static void SelectTag(ComboBox box, string tag)
    {
        foreach (object item in box.Items)
        {
            if (item is ComboBoxItem cbi &&
                string.Equals(cbi.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = cbi;
                return;
            }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box, string fallback) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string ?? fallback;

    /// <summary>把"py, cs md"这类输入解析成不含点的后缀列表。</summary>
    private static List<string> ParseExtensions(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        foreach (string part in raw.Split(',', ' ', ';', '\r', '\n', '\t'))
        {
            string ext = part.Trim().TrimStart('.').ToLowerInvariant();
            if (ext.Length > 0 && !list.Contains(ext)) list.Add(ext);
        }
        return list;
    }
}
