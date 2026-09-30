// Copyright (c) 2026 ByteForge
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>编辑行为：行号、换行、当前行高亮、缩进、括号补全、状态栏。</summary>
public sealed partial class EditorPage : Page
{
    public EditorPage()
    {
        InitializeComponent();
        Load();
    }

    private AppSettings Settings => SettingsStore.Current;

    private void Load()
    {
        LineNumbersSwitch.IsOn = Settings.ShowLineNumbers;
        WordWrapSwitch.IsOn = Settings.WordWrap;
        CurrentLineSwitch.IsOn = Settings.HighlightCurrentLine;
        AutoIndentSwitch.IsOn = Settings.AutoIndent;
        AutoBracketsSwitch.IsOn = Settings.AutoCloseBrackets;
        StatusBarSwitch.IsOn = Settings.ShowStatusBar;
        HintBar.IsOpen = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.ShowLineNumbers = LineNumbersSwitch.IsOn;
        settings.WordWrap = WordWrapSwitch.IsOn;
        settings.HighlightCurrentLine = CurrentLineSwitch.IsOn;
        settings.AutoIndent = AutoIndentSwitch.IsOn;
        settings.AutoCloseBrackets = AutoBracketsSwitch.IsOn;
        settings.ShowStatusBar = StatusBarSwitch.IsOn;

        SettingsUi.SaveCurrent(HintBar, "已保存。编辑器下次启动时生效。");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        Load();
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);
}
