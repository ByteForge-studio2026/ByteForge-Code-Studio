// Copyright (c) 2026 ByteForge
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>启动与窗口：是否打开上次文件、记住窗口大小、最近文件条数。</summary>
public sealed partial class StartupPage : Page
{
    public StartupPage()
    {
        InitializeComponent();

        SettingsUi.ConfigureSlider(RecentCountSlider, 0, 30);
        Load();
    }

    private AppSettings Settings => SettingsStore.Current;

    private void Load()
    {
        OpenLastSwitch.IsOn = Settings.OpenLastFileOnStart;
        RememberWindowSwitch.IsOn = Settings.RememberWindowSize;
        RecentCountSlider.Value = Settings.MaxRecentFiles;
        RecentCountText.Text = $"{Settings.MaxRecentFiles} 条";
        HintBar.IsOpen = false;
    }

    private void RecentCountSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        RecentCountText.Text = $"{e.NewValue:0} 条";
        SettingsUi.Hide(HintBar);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.OpenLastFileOnStart = OpenLastSwitch.IsOn;
        settings.RememberWindowSize = RememberWindowSwitch.IsOn;
        settings.MaxRecentFiles = (int)Math.Round(RecentCountSlider.Value);

        if (settings.RecentFiles.Count > Math.Max(0, settings.MaxRecentFiles))
        {
            settings.RecentFiles.RemoveRange(settings.MaxRecentFiles,
                settings.RecentFiles.Count - settings.MaxRecentFiles);
        }

        SettingsUi.SaveCurrent(HintBar, "已保存。");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        Load();
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);
}
