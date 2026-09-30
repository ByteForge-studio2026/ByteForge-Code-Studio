// Copyright (c) 2026 ByteForge
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>保存、预览与性能：默认编码、Markdown 预览位置、语法高亮的体积上限。</summary>
public sealed partial class SavingPage : Page
{
    public SavingPage()
    {
        InitializeComponent();

        SettingsUi.ConfigureSlider(HighlightLimitSlider, 0, 2048);
        Load();
    }

    private AppSettings Settings => SettingsStore.Current;

    private void Load()
    {
        SettingsUi.SelectTag(EncodingBox, Settings.EncodingPreference);
        SettingsUi.SelectTag(MarkdownBox, Settings.MarkdownPreviewMode);
        HighlightLimitSlider.Value = Settings.HighlightMaxFileSizeKb;
        HighlightLimitText.Text = Settings.HighlightMaxFileSizeKb <= 0
            ? "不限"
            : $"{Settings.HighlightMaxFileSizeKb} KB";
        HintBar.IsOpen = false;
    }

    private void HighlightLimitSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        HighlightLimitText.Text = e.NewValue <= 0 ? "不限" : $"{e.NewValue:0} KB";
        SettingsUi.Hide(HintBar);
    }

    private void Setting_Changed(object sender, SelectionChangedEventArgs e) => SettingsUi.Hide(HintBar);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.EncodingPreference = SettingsUi.SelectedTag(EncodingBox) ?? settings.EncodingPreference;
        settings.MarkdownPreviewMode = SettingsUi.SelectedTag(MarkdownBox) ?? settings.MarkdownPreviewMode;
        settings.HighlightMaxFileSizeKb = (int)Math.Round(HighlightLimitSlider.Value);

        SettingsUi.SaveCurrent(HintBar, "已保存。");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        Load();
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);
}
