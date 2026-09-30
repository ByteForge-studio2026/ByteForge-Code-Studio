// Copyright (c) 2026 ByteForge
using System.IO;
using ByteForge.Core;
using ByteForge.Core.Settings;
using ByteForge.FileIO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>文件关联：逐项勾选要用 ByteForge 打开的后缀。</summary>
public sealed partial class AssociationPage : Page
{
    private const int Columns = 4;

    public AssociationPage()
    {
        InitializeComponent();
        BuildGrid(Settings.AssociatedExtensions);
    }

    private AppSettings Settings => SettingsStore.Current;

    private void BuildGrid(IReadOnlyCollection<string> selected)
    {
        ExtensionGrid.Children.Clear();
        ExtensionGrid.ColumnDefinitions.Clear();
        ExtensionGrid.RowDefinitions.Clear();

        for (int i = 0; i < Columns; i++)
        {
            ExtensionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var extensions = AllExtensions();
        int rows = (extensions.Count + Columns - 1) / Columns;
        for (int i = 0; i < rows; i++)
        {
            ExtensionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < extensions.Count; i++)
        {
            string ext = extensions[i];
            var box = new CheckBox
            {
                Content = "." + ext,
                Tag = ext,
                IsChecked = selected.Contains(ext),
                MinWidth = 110,
                Padding = new Thickness(4, 2, 4, 2),
            };

            Grid.SetRow(box, i / Columns);
            Grid.SetColumn(box, i % Columns);
            ExtensionGrid.Children.Add(box);
        }
    }

    private static List<string> AllExtensions() =>
        AppInfo.SourceExtensions.Concat(AppInfo.PlainTextExtensions).Distinct().ToList();

    private List<string> SelectedExtensions() =>
        ExtensionGrid.Children
            .OfType<CheckBox>()
            .Where(box => box.IsChecked == true)
            .Select(box => box.Tag?.ToString() ?? string.Empty)
            .Where(ext => ext.Length > 0)
            .ToList();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var settings = Settings;
        settings.AssociatedExtensions = SelectedExtensions();

        // 关联到设置程序：双击打开是设置，带文件参数时由它转交给编辑器
        string exe = Path.Combine(AppPaths.InstallDirectory, AppInfo.SettingsExecutableName);
        if (!File.Exists(exe)) exe = Path.Combine(AppPaths.InstallDirectory, AppInfo.ExecutableName);

        bool ok = File.Exists(exe)
            ? FileAssociation.Apply(exe, AppPaths.IconPath, settings.AssociatedExtensions)
            : false;

        settings.IsDefaultEditor = settings.AssociatedExtensions.Count > 0 && ok;
        SettingsStore.Save(settings);

        if (settings.AssociatedExtensions.Count == 0)
        {
            SettingsUi.Show(HintBar, "已保存。没有关联任何文件类型。");
        }
        else if (ok)
        {
            SettingsUi.Show(HintBar, "已保存。文件关联已写入当前用户。");
        }
        else
        {
            SettingsUi.Show(HintBar, "已保存，但文件关联失败：未找到编辑器或注册表不可写。", InfoBarSeverity.Warning);
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        SettingsUi.Reset(HintBar);
        BuildGrid(Settings.AssociatedExtensions);
    }

    private void Folder_Click(object sender, RoutedEventArgs e) => SettingsUi.OpenDataFolder(HintBar);

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);

    private void ClearAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool selected)
    {
        foreach (var box in ExtensionGrid.Children.OfType<CheckBox>())
        {
            box.IsChecked = selected;
        }
        SettingsUi.Hide(HintBar);
    }
}
