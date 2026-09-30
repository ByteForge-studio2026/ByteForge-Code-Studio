// Copyright (c) 2026 ByteForge
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>外观：主题、字体、字号。</summary>
public sealed partial class AppearancePage : Page
{
    public AppearancePage()
    {
        InitializeComponent();

        SettingsUi.ConfigureSlider(FontSizeSlider, 9, 28);
        Load();
    }

    private AppSettings Settings => SettingsStore.Current;

    private void Load()
    {
        SettingsUi.SelectTag(ThemeBox, Settings.Theme);
        SettingsUi.SelectContent(FontBox, Settings.FontFamily);
        FontSizeSlider.Value = Settings.FontSize;
        FontSizeText.Text = $"{Settings.FontSize:0}";
    }

    private void FontSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        FontSizeText.Text = $"{e.NewValue:0}";
        SettingsUi.Hide(HintBar);
    }

    private void Setting_Changed(object sender, SelectionChangedEventArgs e) => SettingsUi.Hide(HintBar);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.Theme = SettingsUi.SelectedTag(ThemeBox) ?? settings.Theme;
        settings.FontFamily = SettingsUi.SelectedContent(FontBox) ?? settings.FontFamily;
        settings.FontSize = FontSizeSlider.Value;

        // 主题立即生效，设置窗口与编辑器保持一致（编辑器下次打开时同步）
        if (Microsoft.UI.Xaml.Application.Current is App app) app.ApplyThemeFromSettings();

        SettingsUi.SaveCurrent(HintBar, "已保存。主题已立即切换；编辑器下次打开时同步。");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        Load();
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);
}
