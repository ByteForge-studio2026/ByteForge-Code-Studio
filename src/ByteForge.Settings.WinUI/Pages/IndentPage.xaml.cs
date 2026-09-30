// Copyright (c) 2026 ByteForge
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>缩进与换行：Tab 宽度、换行符、保存时的清理动作。</summary>
public sealed partial class IndentPage : Page
{
    public IndentPage()
    {
        InitializeComponent();

        SettingsUi.ConfigureSlider(TabSizeSlider, 1, 8);
        Load();
    }

    private AppSettings Settings => SettingsStore.Current;

    private void Load()
    {
        TabSizeSlider.Value = Settings.TabSize;
        TabSizeText.Text = $"{Settings.TabSize}";
        TabToSpacesSwitch.IsOn = Settings.TabToSpaces;
        SettingsUi.SelectTag(LineEndingBox, Settings.LineEnding);
        TrimTrailingSwitch.IsOn = Settings.TrimTrailingWhitespaceOnSave;
        FinalNewlineSwitch.IsOn = Settings.InsertFinalNewlineOnSave;
        HintBar.IsOpen = false;
    }

    private void TabSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        TabSizeText.Text = $"{e.NewValue:0}";
        SettingsUi.Hide(HintBar);
    }

    private void Setting_Changed(object sender, SelectionChangedEventArgs e) => SettingsUi.Hide(HintBar);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.TabSize = (int)Math.Round(TabSizeSlider.Value);
        settings.TabToSpaces = TabToSpacesSwitch.IsOn;
        settings.LineEnding = SettingsUi.SelectedTag(LineEndingBox) ?? settings.LineEnding;
        settings.TrimTrailingWhitespaceOnSave = TrimTrailingSwitch.IsOn;
        settings.InsertFinalNewlineOnSave = FinalNewlineSwitch.IsOn;

        SettingsUi.SaveCurrent(HintBar, "已保存。");
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        Load();
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);
}
