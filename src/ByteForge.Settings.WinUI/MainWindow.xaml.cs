// Copyright (c) 2026 ByteForge
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ByteForge.Settings.WinUI;

/// <summary>
/// 主程序（ByteForgeSettings.exe）：正常双击打开是设置；带文件参数时转交给编辑器。
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "ByteForge Code Studio 设置";
        TitleBarText.Text = "ByteForge Code Studio — 设置";

        SetupTitleBar();
        LoadTitleBarIcon();

        // 默认停在"设置 → 外观"，并让分组展开，子项一眼可见
        SettingsGroup.IsExpanded = true;
        Nav.SelectedItem = AppearanceItem;
        ContentFrame.Navigate(typeof(Pages.AppearancePage));

        Diagnostics.Log("MainWindow ready");
    }

    /// <summary>把窗口内容延伸到标题栏区域，用自己的一行做标题栏。</summary>
    private void SetupTitleBar()
    {
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

            // 系统按钮透明，跟着窗口背景走；hover/pressed 用轻微黑色蒙层（浅色主题下够用）
            var bar = appWindow.TitleBar;
            bar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            bar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            bar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(0x18, 0x00, 0x00, 0x00);
            bar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(0x28, 0x00, 0x00, 0x00);

            // 标题栏右侧给三个系统按钮留足空间，避免内容重叠
            AppTitleBar.Padding = new Thickness(14, 0, 132, 0);
        }
        catch (Exception ex)
        {
            Diagnostics.Log("SetupTitleBar failed: " + ex.Message);
        }
    }

    private void LoadTitleBarIcon()
    {
        try
        {
            TitleBarIcon.Source = IconSource.Get(32);
        }
        catch
        {
            // 图标拿不到就算了，标题文字还在
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag) return;

        ContentFrame.Navigate(PageFor(tag));
    }

    private static System.Type PageFor(string tag) => tag switch
    {
        "editor" => typeof(Pages.EditorPage),
        "indent" => typeof(Pages.IndentPage),
        "saving" => typeof(Pages.SavingPage),
        "startup" => typeof(Pages.StartupPage),
        "association" => typeof(Pages.AssociationPage),
        "about" => typeof(Pages.AboutPage),
        _ => typeof(Pages.AppearancePage),
    };
}
