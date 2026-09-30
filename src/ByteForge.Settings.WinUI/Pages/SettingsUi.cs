// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using ByteForge.Core;
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>
/// 各个设置分页共用的小工具：Slider 范围、ComboBox 取值、保存 / 恢复默认 / 打开配置目录。
/// </summary>
internal static class SettingsUi
{
    /// <summary>Slider 的 Minimum 写在 XAML 里会解析失败，范围统一在这里设（先 Maximum 后 Minimum）。</summary>
    public static void ConfigureSlider(Slider slider, double minimum, double maximum)
    {
        slider.Maximum = maximum;
        slider.Minimum = minimum;
        slider.StepFrequency = 1;
    }

    public static void Show(InfoBar bar, string message, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        bar.Severity = severity;
        bar.Message = message;
        bar.IsOpen = true;
    }

    public static void Hide(InfoBar bar) => bar.IsOpen = false;

    public static void SaveCurrent(InfoBar bar, string message)
    {
        SettingsStore.Save(SettingsStore.Current);
        Show(bar, message);
    }

    public static void Reset(InfoBar bar)
    {
        SettingsStore.Save(new AppSettings());
        Show(bar, "已恢复默认设置（文件关联未改动）。", InfoBarSeverity.Informational);
    }

    public static void OpenDataFolder(InfoBar bar)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.DataDirectory) { UseShellExecute = true });
        }
        catch
        {
            Show(bar, "无法打开配置目录。", InfoBarSeverity.Error);
        }
    }

    public static void SelectTag(ComboBox box, string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        Select(box, item => string.Equals(ItemTag(item), tag, StringComparison.OrdinalIgnoreCase));
    }

    public static void SelectContent(ComboBox box, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        Select(box, item => string.Equals(item.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase));
    }

    public static string? SelectedTag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    public static string? SelectedContent(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Content?.ToString();

    private static void Select(ComboBox box, Func<ComboBoxItem, bool> predicate)
    {
        foreach (object item in box.Items)
        {
            if (item is ComboBoxItem comboItem && predicate(comboItem))
            {
                box.SelectedItem = comboItem;
                return;
            }
        }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static string? ItemTag(ComboBoxItem item) => item.Tag?.ToString();
}
