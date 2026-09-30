// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ByteForge.Core;
using ByteForge.Core.Settings;
using ByteForge.Editor.Wpf.Controls;
using ByteForge.Editor.Wpf.Preview;
using ByteForge.FileIO;
using ByteForge.Markdown;
using ByteForge.Syntax;

namespace ByteForge.Editor.Wpf;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = SettingsStore.Current;
    private readonly DispatcherTimer _previewTimer;

    private string? _filePath;
    private Encoding _encoding = new UTF8Encoding(false);
    private string _lineEnding = "\r\n";
    private bool _hasBom;
    private bool _isDark;
    private bool _sidePanelVisible = true;
    private bool _welcome = true;
    private bool _findOpen;
    private bool _findMatchCase;

    private Action? _dialogOnYes;
    private Action? _dialogOnNo;

    public MainWindow(string? filePath)
    {
        InitializeComponent();

        StateChanged += OnStateChanged;

        LoadIcons();
        ApplySettings();
        ApplyTheme();
        RefreshRecentList();

        Editor.TextChanged += (_, _) =>
        {
            UpdateStatus();
            SchedulePreviewRefresh();
        };
        Editor.CaretChanged += (_, _) => UpdateCaretStatus();

        SettingsView.Closed += (_, _) => SettingsOverlay.Visibility = Visibility.Collapsed;
        SettingsView.Saved += (_, _) => ApplySavedSettings();

        // 内置文件管理器：选中文件就交回这里打开
        FileBrowser.Closed += () => FileBrowserOverlay.Visibility = Visibility.Collapsed;
        FileBrowser.FileOpened += path =>
        {
            FileBrowserOverlay.Visibility = Visibility.Collapsed;
            if (!PromptSaveIfDirty()) return;
            OpenFile(path);
        };

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            RefreshPreview();
        };

        ApplyPreviewLayout();

        // 这是"打开文件"的工具，不是"创建文件"的：没带参数就停在欢迎页，
        // 想打开别的就用活动栏第一个按钮（内置文件管理器，Ctrl+O）自己挑。
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            OpenFile(filePath);
        }
        else if (_settings.OpenLastFileOnStart
                 && !string.IsNullOrEmpty(_settings.LastOpenFile)
                 && File.Exists(_settings.LastOpenFile))
        {
            OpenFile(_settings.LastOpenFile);
        }
        else
        {
            ShowWelcome(true);
        }

        UpdateStatus();
    }

    // ------------------------------------------------------------ 图标 / 主题 / 设置

    private void LoadIcons()
    {
        try
        {
            // 用 PNG（1248x1248）而不是 ico：ico 第一帧只有 16x16，解码后放大会发虚
            string path = AppPaths.DisplayIconPath;
            if (File.Exists(path))
            {
                var uri = new Uri(path, UriKind.Absolute);
                // 按目标尺寸解码，避免把 1248x1248 大图硬缩发虚
                Icon = new BitmapImage(uri) { DecodePixelWidth = 32 };
                LogoIcon.Source = new BitmapImage(uri) { DecodePixelWidth = 32 };
                WelcomeIcon.Source = new BitmapImage(uri) { DecodePixelWidth = 128 };
            }
        }
        catch
        {
            // 图标缺失不影响使用
        }
    }

    private void ApplySettings()
    {
        if (_settings.RememberWindowSize)
        {
            Width = Math.Max(MinWidth, _settings.WindowWidth);
            Height = Math.Max(MinHeight, _settings.WindowHeight);
            if (_settings.WindowMaximized) WindowState = WindowState.Maximized;
        }

        StatusBar.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;

        Editor.ApplySettings(_settings);
    }

    private void ApplyTheme()
    {
        _isDark = string.Equals(_settings.ResolvedTheme(), "dark", StringComparison.OrdinalIgnoreCase);
        Editor.ApplyTheme(SyntaxTheme.FromName(_isDark ? "dark" : "light"), _isDark);

        string text = _isDark ? "#DEDEDE" : "#3A3A3A";
        string statusText = "#FFFFFF";
        string statusBar = _isDark ? "#2D5A9E" : "#4C8BF5";
        string chrome = _isDark ? "#3C3C3C" : "#F6F6F7";
        string activity = _isDark ? "#333333" : "#EFEFF1";
        string side = _isDark ? "#252526" : "#F9F9FA";
        string editor = _isDark ? "#1E1E1E" : "#FFFFFF";
        string edge = _isDark ? "#2A2A2A" : "#E4E4E7";
        // 窗口底色（卡片之间的缝隙）：主体那张卡片自己用 editor 色
        string shell = _isDark ? "#1B1B1D" : "#F2F2F4";

        Background = Solid(shell);
        MainCard.Background = Solid(editor);
        MainCard.BorderBrush = Solid(edge);
        TopBar.Background = Solid(chrome);
        TopBar.BorderBrush = Solid(edge);
        ActivityBar.Background = Solid(activity);
        SidePanel.Background = Solid(side);
        SidePanel.BorderBrush = Solid(_isDark ? "#2A2A2A" : "#E9E9EC");
        TabBar.Background = Solid(_isDark ? "#2D2D2D" : "#F2F2F4");
        TabBar.BorderBrush = Solid(edge);
        BreadcrumbBar.Background = Solid(editor);
        StatusBar.Background = Solid(statusBar);

        AppTitle.Foreground = Solid(text);
        WelcomeSubtitle.Foreground = Solid(text);
        SideHeader.Foreground = Solid(text);
        BreadcrumbText.Foreground = Solid(text);
        TabFileText.Foreground = Solid(text);
        WelcomeHint.Foreground = Solid(text);

        StatusFile.Foreground = Solid(statusText);
        StatusCaret.Foreground = Solid(statusText);
        StatusLanguage.Foreground = Solid(statusText);
        StatusEncoding.Foreground = Solid(statusText);
        StatusSize.Foreground = Solid(statusText);
        StatusHint.Foreground = Solid("#FFFFFF");

        // 左侧活动栏图标（保存 / 预览等已挪到这里，浅色深色跟随）
        string actIcon = _isDark ? "#C7C7C7" : "#5A5A62";
        ActFileGlyph.Stroke = Solid(actIcon);
        ActSideGlyph.Stroke = Solid(actIcon);
        ActSaveGlyph.Stroke = Solid(actIcon);
        ActPreviewGlyph.Stroke = Solid(actIcon);
        ActSettingsGlyph.Stroke = Solid(actIcon);

        PreviewCard.Background = Solid(_isDark ? "#252526" : "#F9F9FA");
        PreviewBox.Foreground = Solid(_isDark ? "#E6E6E6" : "#1F1F1F");

        // 自定义顶栏的窗口控制按钮 / 分隔线 / 弹窗（浅色深色跟随全局）
        string glyph = _isDark ? "#DEDEDE" : "#3A3A3A";
        MinGlyph.Stroke = Solid(glyph);
        MaxGlyph.Stroke = Solid(glyph);
        CloseGlyph.Stroke = Solid(glyph);
        TitleSeparator.Fill = Solid(_isDark ? "#3A3A3A" : "#D8D8DC");

        // 应用内弹窗 / 另存为面板
        DialogCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        DialogCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        DialogTitle.Foreground = Solid(glyph);
        DialogMessage.Foreground = Solid(glyph);
        SaveAsCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        SaveAsCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        SaveAsPath.Background = Solid(_isDark ? "#1E1E1E" : "#FFFFFF");
        SaveAsPath.Foreground = Solid(glyph);
        SaveAsPath.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#D0D0D4");

        SettingsCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        SettingsCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        SettingsView.ApplyTheme(_isDark);

        // 侧栏头部两个按钮（新建组 / 清空）
        string sideDim = _isDark ? "#9A9AA2" : "#8A8A8A";
        string sideEdge = _isDark ? "#3F3F46" : "#D8D8DC";
        NewGroupButton.Foreground = Solid(sideDim);
        NewGroupButton.BorderBrush = Solid(sideEdge);
        ClearRecentButton.Foreground = Solid(sideDim);
        ClearRecentButton.BorderBrush = Solid(sideEdge);

        // 应用内文本输入弹窗（新建 / 重命名分组）
        InputCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        InputCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        InputTitle.Foreground = Solid(glyph);
        InputLabel.Foreground = Solid(glyph);
        InputBox.Background = Solid(_isDark ? "#1E1E1E" : "#FFFFFF");
        InputBox.Foreground = Solid(glyph);
        InputBox.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#D0D0D4");
        InputBox.CaretBrush = Solid(glyph);

        // 顶栏历史记录下拉
        HistoryCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        HistoryCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        HistoryTitle.Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        HistoryClear.Foreground = Solid(_isDark ? "#9A9AA2" : "#8A8A8A");
        HistoryClear.BorderBrush = Solid(_isDark ? "#3F3F46" : "#D8D8DC");

        // 圆角右键菜单的配色（菜单项高亮 / 底色 / 分隔线）
        var resources = System.Windows.Application.Current.Resources;
        resources["MenuBackground"] = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        resources["MenuForeground"] = Solid(_isDark ? "#E6E6E6" : "#1F1F1F");
        resources["MenuBorder"] = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        resources["MenuSeparator"] = Solid(_isDark ? "#3A3A3A" : "#ECECEC");
        resources["MenuHighlight"] = Solid(_isDark ? "#3A6FD8" : "#2F6FEB");
        resources["MenuHighlightText"] = Solid("#FFFFFF");

        FileBrowser.ApplyTheme(_isDark);

        // 应用内"添加到分组"面板（从最近打开多选）
        AddToGroupCard.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        AddToGroupCard.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        AddToGroupTitle.Foreground = Solid(glyph);
        AddToGroupHint.Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        var atgBorder = (Border)AddToGroupList.Parent;
        atgBorder.Background = Solid(_isDark ? "#262626" : "#F7F7F9");
        atgBorder.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        string secFg = _isDark ? "#C7C7C7" : "#8A8A8A";
        string secEdge = _isDark ? "#3A3A3A" : "#D8D8DC";
        AddToGroupClose.Foreground = Solid(secFg);
        AddToGroupClose.BorderBrush = Solid(secEdge);
        AddToGroupCancel.Foreground = Solid(secFg);
        AddToGroupCancel.BorderBrush = Solid(secEdge);
        AddToGroupSelectAll.Foreground = Solid(secFg);
        AddToGroupSelectAll.BorderBrush = Solid(secEdge);

        // 顶栏字符搜索框（在 WindowChrome 的标题区里，必须能接收鼠标）
        TopSearchCard.Background = Solid(_isDark ? "#2B2B2B" : "#FFFFFF");
        TopSearchCard.BorderBrush = Solid(_isDark ? "#3F3F46" : "#D8D8DC");
        TopSearchBox.Foreground = Solid(glyph);
        TopSearchBox.CaretBrush = Solid(glyph);
        TopSearchIcon.Stroke = Solid(_isDark ? "#9A9AA2" : "#8A8A8A");
        TopSearchPlaceholder.Foreground = Solid(_isDark ? "#9A9AA2" : "#8A8A8A");

        // 应用内查找结果面板（输入框已挪到顶栏，这里只留结果）
        FindPanel.Background = Solid(_isDark ? "#2D2D2D" : "#FFFFFF");
        FindPanel.BorderBrush = Solid(_isDark ? "#3A3A3A" : "#E4E4E7");
        FindCount.Foreground = Solid(_isDark ? "#C7C7C7" : "#8A8A8A");
        FindPrevGlyph.Stroke = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        FindNextGlyph.Stroke = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        FindCloseGlyph.Stroke = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        ApplyFindMatchCaseStyle();

        // 顶栏按钮 hover 色（浅色压暗、深色提亮）
        Resources["CaptionHover"] = Solid(_isDark ? "#20FFFFFF" : "#1F000000");
        Resources["CaptionCloseHover"] = Solid("#E81123");
    }

    private static System.Windows.Media.SolidColorBrush Solid(string hex)
    {
        var brush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    // ------------------------------------------------------------ 视图切换

    private void ShowWelcome(bool welcome)
    {
        _welcome = welcome;
        WelcomePanel.Visibility = welcome ? Visibility.Visible : Visibility.Collapsed;
        ContentGrid.Visibility = welcome ? Visibility.Collapsed : Visibility.Visible;

        // 没打开文件时，标签栏 / 面包屑 / 保存都没有意义
        TabBar.Visibility = welcome ? Visibility.Collapsed : Visibility.Visible;
        BreadcrumbBar.Visibility = welcome ? Visibility.Collapsed : Visibility.Visible;
        ActSaveButton.Visibility = welcome ? Visibility.Collapsed : Visibility.Visible;

        // 搜索框常驻顶栏；没打开文件时禁用并改占位提示
        TopSearchBox.IsEnabled = !welcome;
        TopSearchPlaceholder.Text = welcome ? "搜索字符（打开文件后可用）" : "搜索字符 (Ctrl+F)";
        if (welcome) CloseFind();

        // 预览只在打开 Markdown 文件时才有意义
        UpdatePreviewControls();
        ApplyPreviewLayout();
    }

    private void UpdatePreviewControls()
    {
        bool show = !_welcome && Editor.LanguageId == "markdown";
        ActPreviewButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleSidePanel_Click(object sender, RoutedEventArgs e)
    {
        _sidePanelVisible = !_sidePanelVisible;
        ApplySidePanel();
    }

    private void ApplySidePanel()
    {
        // 有最近文件、或者已经建了分组时才显示侧栏，避免出现大片空白
        bool show = _sidePanelVisible
                    && (_settings.RecentFiles.Any(File.Exists) || _settings.RecentGroups.Count > 0);
        SideColumn.Width = show ? new GridLength(236) : new GridLength(0);
        SidePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        // 关掉标签 = 回到欢迎页（不创建任何新文件）
        if (!PromptSaveIfDirty()) return;

        _filePath = null;
        Editor.LoadText(string.Empty, languageId: "plaintext");
        Editor.SetDirty(false);
        CloseFind();
        ShowWelcome(true);
        UpdateStatus();
    }

    // ------------------------------------------------------------ 最近打开

    private void RefreshRecentList()
    {
        RecentList.Items.Clear();

        var files = _settings.RecentFiles.Where(File.Exists).Take(_settings.MaxRecentFiles).ToList();
        bool hasFiles = files.Count > 0;
        bool hasGroups = _settings.RecentGroups.Count > 0;

        SideHeader.Visibility = hasFiles || hasGroups ? Visibility.Visible : Visibility.Collapsed;
        ClearRecentButton.Visibility = hasFiles ? Visibility.Visible : Visibility.Collapsed;
        ApplySidePanel();

        // 0) 置顶的文件：单独一节放在最上面（置顶后就不再出现在原分组里，避免重复）
        var pinned = _settings.PinnedRecentFiles.Where(files.Contains).ToList();
        var normal = files.Where(f => !pinned.Contains(f)).ToList();

        if (pinned.Count > 0)
        {
            RecentList.Items.Add(MakeGroupItem("置顶", pinned.Count, custom: false));
            if (!_settings.IsGroupCollapsed("置顶"))
            {
                foreach (string file in pinned) RecentList.Items.Add(MakeFileItem(file));
            }
        }

        // 1) 自建分组：置顶的组排最前，其余按建组顺序；组内沿用"最近打开"的先后顺序
        var orderedGroups = _settings.PinnedGroups.Where(_settings.RecentGroups.Contains)
            .Concat(_settings.RecentGroups.Where(g => !_settings.IsGroupPinned(g)));

        foreach (string group in orderedGroups)
        {
            var inGroup = normal
                .Where(f => string.Equals(_settings.GroupOf(f), group, StringComparison.OrdinalIgnoreCase))
                .ToList();

            RecentList.Items.Add(MakeGroupItem(group, inGroup.Count, custom: true));

            if (_settings.IsGroupCollapsed(group)) continue;
            foreach (string file in inGroup) RecentList.Items.Add(MakeFileItem(file));
        }

        // 2) 没进任何自建分组的文件：仍按"最后一次打开的时间"自动分组
        var rest = normal.Where(f => string.IsNullOrEmpty(_settings.GroupOf(f))).ToList();
        var counts = rest.GroupBy(f => GroupNameOf(_settings.RecentTimeOf(f)))
                         .ToDictionary(g => g.Key, g => g.Count());

        string? current = null;
        foreach (string file in rest)
        {
            string name = GroupNameOf(_settings.RecentTimeOf(file));
            if (!string.Equals(name, current, StringComparison.Ordinal))
            {
                current = name;
                RecentList.Items.Add(MakeGroupItem(name, counts.TryGetValue(name, out int c) ? c : 0, custom: false));
            }

            if (_settings.IsGroupCollapsed(name)) continue;
            RecentList.Items.Add(MakeFileItem(file));
        }
    }

    // ------------------------------------------------------------ 分组

    private void NewGroup_Click(object sender, RoutedEventArgs e) => NewGroupFor(null);

    /// <summary>新建分组；传了 file 就把该文件直接放进新组。</summary>
    private void NewGroupFor(string? file)
    {
        ShowInput(
            "新建分组",
            string.IsNullOrEmpty(file) ? "分组名称" : "分组名称（文件会自动放进去）",
            string.Empty,
            name =>
            {
                _settings.AddRecentGroup(name);
                if (!string.IsNullOrEmpty(file)) _settings.SetRecentFileGroup(file!, name);
                SettingsStore.Save(_settings);
                RefreshRecentList();
            });
    }

    private void RenameGroup(string name)
    {
        ShowInput("重命名分组", "分组名称", name, next =>
        {
            _settings.RenameRecentGroup(name, next);
            SettingsStore.Save(_settings);
            RefreshRecentList();
        });
    }

    private void DeleteGroup(string name)
    {
        ShowConfirm(
            "删除分组",
            $"删除分组“{name}”？组内的文件不会被删除，只是回到按时间自动分组。",
            () =>
            {
                _settings.RemoveRecentGroup(name);
                SettingsStore.Save(_settings);
                RefreshRecentList();
            });
    }

    private void ToggleGroup(string name)
    {
        _settings.ToggleGroupCollapsed(name);
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    /// <summary>置顶 / 取消置顶一个文件。</summary>
    private void ToggleFilePinned(string file)
    {
        _settings.ToggleFilePinned(file);
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    /// <summary>置顶 / 取消置顶一个自建分组。</summary>
    private void ToggleGroupPinned(string name)
    {
        _settings.ToggleGroupPinned(name);
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    private void MoveFileToGroup(string file, string? group)
    {
        _settings.SetRecentFileGroup(file, group);
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    /// <summary>文件右键菜单：移到哪个组 / 新建组 / 移出组 / 从列表删除。</summary>
    private ContextMenu MakeFileMenu(string file)
    {
        string current = _settings.GroupOf(file);

        var menu = new ContextMenu
        {
            Style = (Style)FindResource("RoundedContextMenu"),
        };

        var move = new MenuItem { Header = "移到分组" };
        foreach (string group in _settings.RecentGroups)
        {
            string target = group;
            move.Items.Add(new MenuItem
            {
                Header = group,
                IsCheckable = true,
                IsChecked = string.Equals(group, current, StringComparison.OrdinalIgnoreCase),
            });
            ((MenuItem)move.Items[move.Items.Count - 1]!).Click += (_, _) => MoveFileToGroup(file, target);
        }
        move.IsEnabled = _settings.RecentGroups.Count > 0;
        menu.Items.Add(move);

        var create = new MenuItem { Header = "新建分组…" };
        create.Click += (_, _) => NewGroupFor(file);
        menu.Items.Add(create);

        if (!string.IsNullOrEmpty(current))
        {
            var leave = new MenuItem { Header = $"移出“{current}”（回到按时间分组）" };
            leave.Click += (_, _) => MoveFileToGroup(file, null);
            menu.Items.Add(leave);
        }

        menu.Items.Add(MakeSeparator());

        var pin = new MenuItem { Header = _settings.IsFilePinned(file) ? "取消置顶" : "置顶" };
        pin.Click += (_, _) => ToggleFilePinned(file);
        menu.Items.Add(pin);

        menu.Items.Add(MakeSeparator());

        var remove = new MenuItem { Header = "从最近列表删除" };
        remove.Click += (_, _) => RemoveRecent(file);
        menu.Items.Add(remove);

        return menu;
    }

    private Separator MakeSeparator() => new() { Style = (Style)FindResource("RoundedSeparator") };

    /// <summary>分组右键菜单：往组里加文件 / 重命名 / 置顶 / 折叠 / 删除。</summary>
    private ContextMenu MakeGroupMenu(string name, bool custom)
    {
        var menu = new ContextMenu { Style = (Style)FindResource("RoundedContextMenu") };

        if (custom)
        {
            var add = new MenuItem { Header = "添加文件到此组…" };
            add.Click += (_, _) => AddFilesToGroup(name);
            menu.Items.Add(add);

            var rename = new MenuItem { Header = "重命名分组…" };
            rename.Click += (_, _) => RenameGroup(name);
            menu.Items.Add(rename);

            var pin = new MenuItem { Header = _settings.IsGroupPinned(name) ? "取消置顶" : "置顶此组" };
            pin.Click += (_, _) => ToggleGroupPinned(name);
            menu.Items.Add(pin);

            var remove = new MenuItem { Header = "删除分组（文件保留）" };
            remove.Click += (_, _) => DeleteGroup(name);
            menu.Items.Add(remove);

            menu.Items.Add(MakeSeparator());
        }

        var toggle = new MenuItem { Header = _settings.IsGroupCollapsed(name) ? "展开" : "折叠" };
        toggle.Click += (_, _) => ToggleGroup(name);
        menu.Items.Add(toggle);

        return menu;
    }

    /// <summary>待加入分组的目标组（应用内"添加到分组"面板用）。</summary>
    private string? _pendingGroup;

    /// <summary>从"最近打开"里多选文件，登记进某个分组（应用内，不弹系统文件框）。</summary>
    private void AddFilesToGroup(string group)
    {
        _pendingGroup = group;

        AddToGroupTitle.Text = $"添加到分组“{group}”";
        AddToGroupHint.Visibility = Visibility.Collapsed;
        AddToGroupList.Items.Clear();

        var files = _settings.RecentFiles.Where(File.Exists).Take(_settings.MaxRecentFiles).ToList();

        if (files.Count == 0)
        {
            // 还没打开过任何文件：先引导用户用文件管理器打开，它们才会出现在这里
            AddToGroupHint.Text = "最近打开为空。先打开几个文件，它们会出现在这里供你加入分组。";
            AddToGroupHint.Visibility = Visibility.Visible;
            AddToGroupConfirm.IsEnabled = false;
            AddToGroupSelectAll.IsEnabled = false;
        }
        else
        {
            AddToGroupConfirm.IsEnabled = true;
            AddToGroupSelectAll.IsEnabled = true;

            foreach (string file in files)
            {
                var cb = new CheckBox
                {
                    Tag = file,
                    Margin = new Thickness(4, 3, 4, 3),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    // 已经在这个组里的默认勾上，方便追加
                    IsChecked = string.Equals(_settings.GroupOf(file), group, StringComparison.OrdinalIgnoreCase),
                };

                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(file),
                    FontSize = 13,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = Solid(_isDark ? "#E6E6E6" : "#1F1F1F"),
                });
                stack.Children.Add(new TextBlock
                {
                    Text = file,
                    FontSize = 11,
                    Opacity = 0.55,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = Solid(_isDark ? "#C7C7C7" : "#5A5A62"),
                });
                cb.Content = stack;

                AddToGroupList.Items.Add(cb);
            }
        }

        AddToGroupOverlay.Visibility = Visibility.Visible;
    }

    private void AddToGroupOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid) HideAddToGroup();
    }

    private void AddToGroupClose_Click(object sender, RoutedEventArgs e) => HideAddToGroup();
    private void AddToGroupCancel_Click(object sender, RoutedEventArgs e) => HideAddToGroup();

    private void AddToGroupSelectAll_Click(object sender, RoutedEventArgs e)
    {
        bool allChecked = true;
        foreach (CheckBox cb in AddToGroupList.Items)
        {
            if (cb.IsChecked != true) { allChecked = false; break; }
        }

        foreach (CheckBox cb in AddToGroupList.Items) cb.IsChecked = !allChecked;
    }

    private void AddToGroupConfirm_Click(object sender, RoutedEventArgs e)
    {
        string? group = _pendingGroup;
        if (string.IsNullOrEmpty(group)) { HideAddToGroup(); return; }

        foreach (CheckBox cb in AddToGroupList.Items)
        {
            if (cb.IsChecked != true || cb.Tag is not string file) continue;
            _settings.PushRecentFile(file);
            _settings.SetRecentFileGroup(file, group);
        }

        SettingsStore.Save(_settings);
        RefreshRecentList();
        HideAddToGroup();
    }

    private void HideAddToGroup()
    {
        AddToGroupOverlay.Visibility = Visibility.Collapsed;
        _pendingGroup = null;
    }

    /// <summary>侧栏里的一条最近文件：文件名 + 路径 + hover 出现的删除按钮 + 右键分组菜单。</summary>
    private ListBoxItem MakeFileItem(string file)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(file),
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Solid(_isDark ? "#E6E6E6" : "#1F1F1F"),
        });
        panel.Children.Add(new TextBlock
        {
            Text = file,
            FontSize = 11,
            Opacity = 0.55,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Solid(_isDark ? "#C7C7C7" : "#5A5A62"),
        });

        // 右侧删除按钮：hover 才显形，点击从最近列表移除该条
        var remove = new Button
        {
            Content = "✕",
            FontSize = 11,
            Width = 22,
            Height = 22,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0xFF, 0x9A, 0x9A, 0xA2)),
            Opacity = 0,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "从列表删除",
            VerticalAlignment = VerticalAlignment.Center,
        };
        remove.Click += (_, _) => RemoveRecent(file);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(panel, 0);
        Grid.SetColumn(remove, 1);
        row.Children.Add(panel);
        row.Children.Add(remove);

        var item = new ListBoxItem { Content = row, Tag = file };
        item.PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;     // 右键只出菜单，不打开文件
            // 点在删除按钮上时不触发打开
            if (e.OriginalSource is DependencyObject d && FindParent<Button>(d) != null) return;
            if (!PromptSaveIfDirty())
            {
                e.Handled = true;
                return;
            }
            OpenFile(file);
        };
        item.MouseEnter += (_, _) => remove.Opacity = 1;
        item.MouseLeave += (_, _) => remove.Opacity = 0;
        item.ContextMenu = MakeFileMenu(file);

        return item;
    }

    /// <summary>侧栏分组标题：点一下折叠 / 展开；自建组 hover 出重命名与删除。</summary>
    private ListBoxItem MakeGroupItem(string name, int count, bool custom)
    {
        bool collapsed = _settings.IsGroupCollapsed(name);

        var title = new TextBlock
        {
            Text = (collapsed ? "▸ " : "▾ ") + name + (count > 0 ? $"  ({count})" : string.Empty),
            FontSize = 10.5,
            FontWeight = FontWeights.SemiBold,
            Opacity = 0.68,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73"),
        };

        Visibility customOnly = custom ? Visibility.Visible : Visibility.Collapsed;

        // 加文件：把磁盘上的文件登记进这个组（只登记，不打开内容）
        var add = MakeGlyphButton("＋", "把文件加入这个组", customOnly);
        add.Click += (_, _) => AddFilesToGroup(name);

        // 置顶：★ = 已置顶，☆ = 未置顶（只有自建分组能置顶）
        var pin = MakeGlyphButton(_settings.IsGroupPinned(name) ? "★" : "☆",
                                  _settings.IsGroupPinned(name) ? "取消置顶" : "置顶分组",
                                  customOnly);
        pin.Click += (_, _) => ToggleGroupPinned(name);

        var rename = MakeGlyphButton("✎", "重命名分组", customOnly);
        rename.Click += (_, _) => RenameGroup(name);

        var delete = MakeGlyphButton("✕", "删除分组（文件保留）", customOnly);
        delete.Click += (_, _) => DeleteGroup(name);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
        };
        buttons.Children.Add(add);
        buttons.Children.Add(pin);
        buttons.Children.Add(rename);
        buttons.Children.Add(delete);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(title, 0);
        Grid.SetColumn(buttons, 1);
        row.Children.Add(title);
        row.Children.Add(buttons);

        var item = new ListBoxItem
        {
            Content = row,
            Tag = name,
            Margin = new Thickness(12, 6, 12, 2),
            Padding = new Thickness(0),
            Focusable = false,
            Style = (Style)FindResource("RecentGroupItem"),
        };

        item.PreviewMouseLeftButtonDown += (_, e) =>
        {
            // 点在重命名 / 删除按钮上时只执行按钮，不折叠
            if (e.OriginalSource is DependencyObject d && FindParent<Button>(d) != null) return;
            ToggleGroup(name);
            e.Handled = true;        // 分组标题不参与选中
        };
        item.MouseEnter += (_, _) => buttons.Opacity = 1;
        item.MouseLeave += (_, _) => buttons.Opacity = 0;
        item.ContextMenu = MakeGroupMenu(name, custom);

        // 从资源管理器把文件拖到组标题上：直接进这个组（不打开文件）
        if (custom)
        {
            item.AllowDrop = true;
            item.PreviewDragOver += (_, e) =>
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            };
            item.Drop += (_, e) =>
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[]? dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dropped is null) return;

                foreach (string file in dropped.Where(File.Exists))
                {
                    _settings.PushRecentFile(file);
                    _settings.SetRecentFileGroup(file, name);
                }

                SettingsStore.Save(_settings);
                RefreshRecentList();
                e.Handled = true;
            };
        }

        return item;
    }

    private static Button MakeGlyphButton(string glyph, string tip, Visibility visible) => new()
    {
        Content = glyph,
        FontSize = 11,
        Width = 20,
        Height = 20,
        Padding = new Thickness(0),
        Margin = new Thickness(4, 0, 0, 0),
        Background = System.Windows.Media.Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(0xFF, 0x9A, 0x9A, 0xA2)),
        Cursor = System.Windows.Input.Cursors.Hand,
        ToolTip = tip,
        Visibility = visible,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>把一个打开时间归到"今天 / 昨天 / 最近 7 天 / 最近 30 天 / 更早"。</summary>
    private static string GroupNameOf(DateTime time)
    {
        if (time == DateTime.MinValue) return "更早";

        DateTime now = DateTime.Now;
        if (time.Date == now.Date) return "今天";
        if (time.Date == now.Date.AddDays(-1)) return "昨天";
        if (time >= now.Date.AddDays(-6)) return "最近 7 天";
        if (time >= now.Date.AddDays(-29)) return "最近 30 天";
        return "更早";
    }

    private static T? FindParent<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    private void RemoveRecent(string file)
    {
        _settings.RemoveRecentFile(file);
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    private void ClearRecent_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.RecentFiles.Count == 0) return;
        _settings.RecentFiles.Clear();
        _settings.RecentFileTimes.Clear();
        SettingsStore.Save(_settings);
        RefreshRecentList();
    }

    // ------------------------------------------------------------ 顶栏历史记录下拉（类似 IDEA）

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryPopup.IsOpen)
        {
            HistoryPopup.IsOpen = false;
            return;
        }

        RefreshHistoryList();
        HistoryPopup.IsOpen = true;
    }

    /// <summary>重建历史下拉：最近打开的文件，按最近在前排列（RecentFiles 已是有序的）。</summary>
    private void RefreshHistoryList()
    {
        HistoryList.Items.Clear();

        var files = _settings.RecentFiles.Where(File.Exists).Take(_settings.MaxRecentFiles).ToList();
        if (files.Count == 0)
        {
            HistoryList.Items.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = "还没有打开过任何文件",
                    FontSize = 12.5,
                    Opacity = 0.55,
                    Margin = new Thickness(8, 6, 8, 6),
                    Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73"),
                },
                IsHitTestVisible = false,
                Style = (Style)FindResource("FindResultItem"),
            });
            return;
        }

        foreach (string file in files)
        {
            HistoryList.Items.Add(MakeHistoryItem(file));
        }
    }

    private ListBoxItem MakeHistoryItem(string file)
    {
        var name = new TextBlock
        {
            Text = Path.GetFileName(file),
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Solid(_isDark ? "#E6E6E6" : "#1F1F1F"),
        };
        var dir = new TextBlock
        {
            Text = Path.GetDirectoryName(file),
            FontSize = 11,
            Opacity = 0.55,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Solid(_isDark ? "#C7C7C7" : "#5A5A62"),
        };

        var panel = new StackPanel();
        panel.Children.Add(name);
        panel.Children.Add(dir);

        var item = new ListBoxItem
        {
            Content = panel,
            Tag = file,
            Style = (Style)FindResource("FindResultItem"),
            ToolTip = file,
        };
        item.MouseLeftButtonUp += (_, _) =>
        {
            HistoryPopup.IsOpen = false;
            if (!PromptSaveIfDirty()) return;
            OpenFile(file);
        };
        return item;
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.RecentFiles.Count == 0) return;
        _settings.RecentFiles.Clear();
        _settings.RecentFileTimes.Clear();
        SettingsStore.Save(_settings);
        RefreshRecentList();
        RefreshHistoryList();
    }

    // ------------------------------------------------------------ 打开 / 保存

    // ------------------------------------------------------------ 内置文件管理器

    private void FileButton_Click(object sender, RoutedEventArgs e) => OpenFileBrowser();

    private void OpenFileBrowser()
    {
        // 从当前文件 / 最近打开所在的目录起步，其次是主目录
        string? start = string.IsNullOrEmpty(_filePath) ? null : Path.GetDirectoryName(_filePath);
        if (string.IsNullOrEmpty(start) && _settings.RecentFiles.Count > 0)
        {
            start = Path.GetDirectoryName(_settings.RecentFiles[0]);
        }

        FileBrowser.Show(start, _isDark, _settings.AssociatedExtensions);
        FileBrowserOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>点卡片外的灰色区域：关掉文件管理器。</summary>
    private void FileBrowserOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid) FileBrowserOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>打开文件（无后缀按文本处理，二进制给出提示）。</summary>
    public void OpenFile(string path)
    {
        CloseFind();
        try
        {
            var result = TextFileOpener.Open(path);

            _filePath = result.Path;
            _encoding = result.Encoding;
            _lineEnding = result.LineEnding;
            _hasBom = result.HasBom;

            Editor.LoadText(result.Text, languageId: null, filePath: result.Path);

            _settings.PushRecentFile(path);
            SettingsStore.Save(_settings);

            ShowWelcome(false);
            RefreshRecentList();

            StatusHint.Text = result.Warning ?? string.Empty;
            HintPill.Visibility = string.IsNullOrEmpty(StatusHint.Text) ? Visibility.Collapsed : Visibility.Visible;
            UpdateStatus();
        }
        catch (UnsupportedBinaryFileException)
        {
            ShowMessage("无法打开文件", "该文件是不受支持的二进制文件");
        }
        catch (FileOpenException ex)
        {
            ShowMessage("无法打开文件", ex.Message);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => Save();

    private bool Save()
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            // 没有路径：没法静默保存，弹出应用内"另存为"面板（不另开窗口）
            OpenSaveAs();
            return false;
        }

        string target = _filePath;

        try
        {
            TextFileSaver.Save(target, PrepareSaveText(), _encoding, ResolveLineEnding(), _hasBom);
            Editor.SetDirty(false);
            UpdateStatus();
            return true;
        }
        catch (Exception ex)
        {
            ShowMessage("保存", $"保存失败：{ex.Message}");
            return false;
        }
    }

    // 应用内"另存为"面板：直接输入完整路径，不用系统文件框
    private void OpenSaveAs()
    {
        string dir = string.IsNullOrEmpty(_filePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Path.GetDirectoryName(_filePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string name = string.IsNullOrEmpty(_filePath) ? "未命名.txt" : Path.GetFileName(_filePath);
        SaveAsPath.Text = Path.Combine(dir, name);
        SaveAsOverlay.Visibility = Visibility.Visible;
        SaveAsPath.Focus();
        SaveAsPath.SelectAll();
    }

    private void SaveAsCancel_Click(object sender, RoutedEventArgs e) =>
        SaveAsOverlay.Visibility = Visibility.Collapsed;

    private void SaveAsOk_Click(object sender, RoutedEventArgs e)
    {
        string target = SaveAsPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(target)) return;

        try
        {
            string? parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);

            _filePath = target;
            bool bom = string.Equals(_settings.EncodingPreference, "utf8-bom", StringComparison.OrdinalIgnoreCase);
            _encoding = new UTF8Encoding(bom);
            _hasBom = bom;
            _lineEnding = ResolveLineEnding();

            TextFileSaver.Save(target, PrepareSaveText(), _encoding, _lineEnding, _hasBom);
            Editor.SetDirty(false);
            Editor.LoadText(Editor.Text, languageId: null, filePath: _filePath);
            SaveAsOverlay.Visibility = Visibility.Collapsed;
            ShowWelcome(false);
            UpdateStatus();
        }
        catch (Exception ex)
        {
            ShowMessage("保存失败", ex.Message);
        }
    }

    /// <summary>按设置处理要写入的文本（去行尾空格、补末尾换行）。</summary>
    private string PrepareSaveText()
    {
        string text = Editor.Text;

        if (_settings.TrimTrailingWhitespaceOnSave)
        {
            text = Regex.Replace(text, @"[ \t]+(?=\r?\n|$)", string.Empty);
        }

        if (_settings.InsertFinalNewlineOnSave && text.Length > 0 && !text.EndsWith('\n'))
        {
            text += ResolveLineEnding();
        }

        return text;
    }

    /// <summary>保存时使用的换行符：设置优先，keep 时沿用文件原本的换行符。</summary>
    private string ResolveLineEnding() => _settings.LineEnding switch
    {
        "lf" => "\n",
        "crlf" => "\r\n",
        _ => string.IsNullOrEmpty(_lineEnding) ? "\r\n" : _lineEnding,
    };

    // ------------------------------------------------------------ Markdown 预览

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.LanguageId != "markdown") return;

        _settings.MarkdownPreviewMode = _settings.MarkdownPreviewMode switch
        {
            "off" => "bottom",
            "bottom" => "side",
            _ => "off",
        };
        SettingsStore.Save(_settings);
        ApplyPreviewLayout();
        RefreshPreview();
    }

    private void ApplyPreviewLayout()
    {
        // 预览只在打开 Markdown 文件时才有意义；其余文件一律隐藏
        bool md = Editor.LanguageId == "markdown";
        string mode = _settings.MarkdownPreviewMode;
        bool visible = md && mode != "off";

        // 复位为编辑器独占
        Grid.SetRow(Editor, 0);
        Grid.SetColumn(Editor, 0);
        Grid.SetRowSpan(Editor, 3);
        Grid.SetColumnSpan(Editor, 3);
        PreviewRow.Height = new GridLength(0);
        PreviewColumn.Width = new GridLength(0);
        Splitter.Visibility = Visibility.Collapsed;
        PreviewCard.Visibility = Visibility.Collapsed;

        if (!visible) return;

        bool side = mode == "side";

        if (side)
        {
            PreviewRow.Height = new GridLength(0);
            PreviewColumn.Width = new GridLength(Math.Max(240, Width * 0.38));

            Grid.SetRow(Splitter, 0);
            Grid.SetColumn(Splitter, 1);
            Grid.SetRowSpan(Splitter, 3);
            Grid.SetColumnSpan(Splitter, 1);
            Splitter.ResizeDirection = GridResizeDirection.Columns;
            Splitter.Width = 4;
            Splitter.Height = double.NaN;
            Splitter.HorizontalAlignment = HorizontalAlignment.Center;
            Splitter.VerticalAlignment = VerticalAlignment.Stretch;

            Grid.SetRow(Editor, 0);
            Grid.SetColumn(Editor, 0);
            Grid.SetRowSpan(Editor, 3);
            Grid.SetColumnSpan(Editor, 1);

            Grid.SetRow(PreviewCard, 0);
            Grid.SetColumn(PreviewCard, 2);
            Grid.SetRowSpan(PreviewCard, 3);
            Grid.SetColumnSpan(PreviewCard, 1);
            PreviewCard.Margin = new Thickness(0, 10, 10, 10);
        }
        else
        {
            PreviewColumn.Width = new GridLength(0);
            PreviewRow.Height = new GridLength(Math.Max(160, Height * 0.32));

            Grid.SetRow(Splitter, 1);
            Grid.SetColumn(Splitter, 0);
            Grid.SetRowSpan(Splitter, 1);
            Grid.SetColumnSpan(Splitter, 3);
            Splitter.ResizeDirection = GridResizeDirection.Rows;
            Splitter.Height = 4;
            Splitter.Width = double.NaN;
            Splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            Splitter.VerticalAlignment = VerticalAlignment.Stretch;

            Grid.SetRow(Editor, 0);
            Grid.SetColumn(Editor, 0);
            Grid.SetRowSpan(Editor, 1);
            Grid.SetColumnSpan(Editor, 3);

            Grid.SetRow(PreviewCard, 2);
            Grid.SetColumn(PreviewCard, 0);
            Grid.SetRowSpan(PreviewCard, 1);
            Grid.SetColumnSpan(PreviewCard, 3);
            PreviewCard.Margin = new Thickness(0, 0, 10, 10);
        }

        Splitter.Visibility = Visibility.Visible;
        PreviewCard.Visibility = Visibility.Visible;
    }

    private void SchedulePreviewRefresh()
    {
        if (_settings.MarkdownPreviewMode == "off") return;
        if (Editor.LanguageId != "markdown") return;

        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void RefreshPreview()
    {
        if (_settings.MarkdownPreviewMode == "off") return;

        if (Editor.LanguageId != "markdown")
        {
            PreviewBox.Document.Blocks.Clear();
            PreviewBox.Document.Blocks.Add(new System.Windows.Documents.Paragraph(
                new System.Windows.Documents.Run("Markdown 预览仅在打开 .md 文件时可用。")));
            return;
        }

        var document = MarkdownParser.Parse(Editor.Text);
        PreviewBox.Document = MarkdownFlowRenderer.Render(document, _isDark);
    }

    // ------------------------------------------------------------ 设置（已合并进编辑器，应用内打开）

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>设置保存后：重新应用主题、编辑器设置、侧栏与预览。</summary>
    private void ApplySavedSettings()
    {
        StatusBar.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
        Editor.ApplySettings(_settings);
        ApplyTheme();
        ApplySidePanel();
        RefreshRecentList();
        ApplyPreviewLayout();
        RefreshPreview();
        UpdateStatus();
    }

    // ------------------------------------------------------------ 自定义顶栏：窗口控制

    private void MinButton_Click(object sender, RoutedEventArgs e) =>
        System.Windows.SystemCommands.MinimizeWindow(this);

    private void MaxButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
            System.Windows.SystemCommands.RestoreWindow(this);
        else
            System.Windows.SystemCommands.MaximizeWindow(this);
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 双击空白处（非按钮）切换最大化 / 还原；单击交给 WindowChrome 处理拖拽
        if (e.ClickCount != 2) return;
        if (e.OriginalSource is DependencyObject d && FindParent<Button>(d) != null) return;
        ToggleMaximize();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        bool maximized = WindowState == WindowState.Maximized;
        var conv = new System.Windows.Media.GeometryConverter();
        MaxGlyph.Data = (System.Windows.Media.Geometry)conv.ConvertFromString(
            maximized ? "M8 3v3a2 2 0 0 1-2 2H3m18 0h-3a2 2 0 0 1-2-2V3m0 18v-3a2 2 0 0 1 2-2h3M3 16h3a2 2 0 0 1 2 2v3"
                      : "M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3")!;
        MaxButton.ToolTip = maximized ? "向下还原" : "最大化";
    }

    // ------------------------------------------------------------ 应用内弹窗（不另开窗口）

    private void ShowMessage(string title, string message, string okText = "确定")
    {
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogYes.Content = okText;
        DialogNo.Visibility = Visibility.Collapsed;
        _dialogOnYes = CloseDialog;
        _dialogOnNo = null;
        DialogOverlay.Visibility = Visibility.Visible;
    }

    private void ShowConfirm(string title, string message, Action onYes, string yesText = "是", string noText = "否")
    {
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogYes.Content = yesText;
        DialogNo.Content = noText;
        DialogNo.Visibility = Visibility.Visible;
        _dialogOnYes = () => { CloseDialog(); onYes(); };
        _dialogOnNo = CloseDialog;
        DialogOverlay.Visibility = Visibility.Visible;
    }

    private void CloseDialog()
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
        _dialogOnYes = null;
        _dialogOnNo = null;
    }

    private void DialogYes_Click(object sender, RoutedEventArgs e) => _dialogOnYes?.Invoke();

    private void DialogNo_Click(object sender, RoutedEventArgs e) => _dialogOnNo?.Invoke();

    // ------------------------------------------------------------ 应用内文本输入（分组名称等）

    private Action<string>? _inputOnOk;

    private void ShowInput(string title, string label, string initial, Action<string> onOk)
    {
        InputTitle.Text = title;
        InputLabel.Text = label;
        InputBox.Text = initial;
        _inputOnOk = onOk;
        InputOverlay.Visibility = Visibility.Visible;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void CloseInput()
    {
        InputOverlay.Visibility = Visibility.Collapsed;
        _inputOnOk = null;
    }

    private void InputCancel_Click(object sender, RoutedEventArgs e) => CloseInput();

    private void InputOk_Click(object sender, RoutedEventArgs e) => CommitInput();

    private void CommitInput()
    {
        string text = InputBox.Text.Trim();
        var action = _inputOnOk;
        CloseInput();
        if (!string.IsNullOrEmpty(text)) action?.Invoke(text);
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseInput();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------ 查找面板（应用内，不另开窗口）

    private readonly List<SearchResult> _findResults = new();
    private int _findCurrent = -1;

    private void OpenFind()
    {
        if (_welcome) return;

        _findOpen = true;
        FindPanel.Visibility = Visibility.Visible;

        // 预填：顶栏搜索框空着，且编辑器里正好选了一段不含换行的文本时，直接拿它当搜索词
        string sel = Editor.SelectedText.Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (TopSearchBox.Text.Length == 0 && !string.IsNullOrEmpty(sel)) TopSearchBox.Text = sel;

        TopSearchBox.Focus();
        TopSearchBox.SelectAll();

        if (!string.IsNullOrEmpty(TopSearchBox.Text)) BuildResults();
        else FindCount.Text = string.Empty;
    }

    private void CloseFind()
    {
        if (!_findOpen) return;
        _findOpen = false;
        FindPanel.Visibility = Visibility.Collapsed;
        Editor.ClearMatchHighlight();
        Editor.Focus();
    }

    private void TopSearchBox_GotFocus(object sender, RoutedEventArgs e) => TopSearchBox.SelectAll();

    private void TopSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TopSearchPlaceholder.Visibility =
            string.IsNullOrEmpty(TopSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        // 在顶栏直接敲字就已经开始搜了：结果面板这时自动浮出来
        if (!_welcome && !_findOpen && TopSearchBox.Text.Length > 0) OpenFind();
        if (_findOpen) BuildResults();
    }

    private void TopSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            StepResult(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : +1);
            e.Handled = true;
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => StepResult(+1);
    private void FindPrev_Click(object sender, RoutedEventArgs e) => StepResult(-1);

    private void FindMatchCase_Click(object sender, RoutedEventArgs e)
    {
        _findMatchCase = !_findMatchCase;
        ApplyFindMatchCaseStyle();
        BuildResults();
    }

    private void FindClose_Click(object sender, RoutedEventArgs e) => CloseFind();

    private void BuildResults()
    {
        string term = TopSearchBox.Text;
        FindResults.Items.Clear();
        _findResults.Clear();

        if (string.IsNullOrEmpty(term))
        {
            FindCount.Text = string.Empty;
            _findCurrent = -1;
            Editor.ClearMatchHighlight();
            return;
        }

        _findResults.AddRange(Editor.FindAll(term, _findMatchCase));
        foreach (SearchResult r in _findResults)
            FindResults.Items.Add(MakeResultItem(r, term));

        FindCount.Text = _findResults.Count == 0 ? "无匹配" : $"{_findResults.Count} 个结果";
        FindCount.Foreground = _findResults.Count == 0
            ? Solid("#C0392B")
            : Solid(_isDark ? "#C7C7C7" : "#8A8A8A");
        _findCurrent = -1;
    }

    private void StepResult(int dir)
    {
        if (_findResults.Count == 0) return;
        _findCurrent = (_findCurrent + dir + _findResults.Count) % _findResults.Count;
        JumpToResult(_findResults[_findCurrent]);
    }

    private void JumpToResult(SearchResult r)
    {
        Editor.GoToMatch(r.Line - 1, r.Column, r.Length);
        Editor.Focus();
        foreach (ListBoxItem item in FindResults.Items)
        {
            if (ReferenceEquals(item.Tag, r)) item.IsSelected = true;
        }
    }

    private ListBoxItem MakeResultItem(SearchResult r, string term)
    {
        var lineNo = new TextBlock
        {
            Text = $"行 {r.Line}",
            FontSize = 11,
            Opacity = 0.55,
            Width = 48,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73"),
        };

        var text = new TextBlock
        {
            FontSize = 12.5,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Solid(_isDark ? "#E6E6E6" : "#1F1F1F"),
        };

        StringComparison c = _findMatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int pos = r.LineText.IndexOf(term, c);
        if (pos >= 0)
        {
            if (pos > 0) text.Inlines.Add(new Run(r.LineText.Substring(0, pos)));
            text.Inlines.Add(new Run(r.LineText.Substring(pos, term.Length))
            {
                Background = Solid("#FFE08A"),
                Foreground = Solid("#1F1F1F"),
            });
            if (pos + term.Length < r.LineText.Length)
                text.Inlines.Add(new Run(r.LineText.Substring(pos + term.Length)));
        }
        else
        {
            text.Text = r.LineText;
        }

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(lineNo, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(lineNo);
        row.Children.Add(text);

        var item = new ListBoxItem { Content = row, Tag = r, Style = (Style)FindResource("FindResultItem") };
        item.PreviewMouseLeftButtonDown += (_, _) => JumpToResult(r);
        return item;
    }

    private void ApplyFindMatchCaseStyle()
    {
        if (_findMatchCase)
        {
            FindMatchCase.Background = Solid(_isDark ? "#3A6FB0" : "#4C8BF5");
            FindMatchCaseText.Foreground = Solid("#FFFFFF");
        }
        else
        {
            FindMatchCase.Background = System.Windows.Media.Brushes.Transparent;
            FindMatchCaseText.Foreground = Solid(_isDark ? "#C7C7C7" : "#6E6E73");
        }
    }

    // ------------------------------------------------------------ 状态栏

    private void UpdateStatus()
    {
        if (string.IsNullOrEmpty(_filePath))
        {
            Title = AppInfo.Name;
            StatusFile.Text = "未打开文件";
            return;
        }

        string name = Path.GetFileName(_filePath);
        bool dirty = Editor.IsDirty;

        Title = $"{(dirty ? "*" : string.Empty)}{name} - {AppInfo.Name}";
        StatusFile.Text = dirty ? $"{name} ●" : name;
        TabFileText.Text = name;
        TabDirtyMark.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        BreadcrumbText.Text = Path.GetDirectoryName(_filePath) ?? string.Empty;

        StatusEncoding.Text = _encoding.WebName.ToUpperInvariant() + (_hasBom ? " (BOM)" : string.Empty);
        StatusLanguage.Text = Editor.LanguageDisplayName;
        StatusSize.Text = FormatSize(Encoding.UTF8.GetByteCount(Editor.Text));
        UpdateCaretStatus();
    }

    private void UpdateCaretStatus() =>
        StatusCaret.Text = $"行 {Editor.CaretLine + 1}，列 {Editor.CaretColumn + 1}";

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.##} MB",
    };

    // ------------------------------------------------------------ 交互

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 文件管理器开着时，Esc 先关它
        if (FileBrowserOverlay.Visibility == Visibility.Visible && e.Key == Key.Escape)
        {
            FileBrowserOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
            return;
        }

        // 查找条打开时，Esc 直接关掉（无论焦点在哪儿）
        if (_findOpen && e.Key == Key.Escape)
        {
            CloseFind();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (e.Key)
        {
            case Key.S:
                if (!string.IsNullOrEmpty(_filePath)) Save();
                e.Handled = true;
                break;
            case Key.F:
                if (!_welcome) OpenFind();
                e.Handled = true;
                break;
            case Key.O:
                OpenFileBrowser();
                e.Handled = true;
                break;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e)
    {
        bool hasFile = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;

        // 拖到某个自建分组标题上：交给分组自己处理（把文件收进组，不打开内容）
        if (e.OriginalSource is DependencyObject src
            && FindParent<ListBoxItem>(src) is ListBoxItem groupItem
            && groupItem.Tag is string group
            && _settings.RecentGroups.Contains(group))
        {
            return;
        }

        if (!PromptSaveIfDirty()) return;
        OpenFile(files[0]);
        e.Handled = true;
    }

    private bool PromptSaveIfDirty()
    {
        // 有修改且文件路径明确时直接保存，不弹询问
        if (Editor.IsDirty && !string.IsNullOrEmpty(_filePath))
        {
            return Save();
        }

        return true;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!PromptSaveIfDirty())
        {
            e.Cancel = true;
            return;
        }

        if (_settings.RememberWindowSize)
        {
            _settings.WindowWidth = Width;
            _settings.WindowHeight = Height;
            _settings.WindowMaximized = WindowState == WindowState.Maximized;
        }

        _settings.LastOpenFile = _filePath ?? string.Empty;
        SettingsStore.Save(_settings);
    }
}
