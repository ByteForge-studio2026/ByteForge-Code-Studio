// Copyright (c) 2026 ByteForge
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ByteForge.Editor.Wpf.Controls;

/// <summary>
/// 应用内文件管理器：不用系统的文件对话框，自己在窗口里列目录、挑文件。
/// 双击文件（或选中后点"打开"）就把路径交给宿主去打开。
/// </summary>
public partial class FileBrowserPanel : UserControl
{
    private const string FolderGlyph = "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z";
    private const string FileGlyph = "M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z M13 2 L13 9 L20 9 L20 9";

    private sealed record Entry(string Path, bool IsDir);

    /// <summary>选中了一个文件，宿主应该打开它。</summary>
    public event Action<string>? FileOpened;

    /// <summary>点关闭 / 取消。</summary>
    public event Action? Closed;

    private string _dir = string.Empty;
    private string? _lastDir;
    private bool _dark;
    private bool _onlyText = true;
    private string _filter = string.Empty;
    // 构造期间 XAML 里的 IsSelected 会提前触发 SelectionChanged，那时列表控件还没建好
    private bool _ready;
    private readonly HashSet<string> _textExtensions = new(System.StringComparer.OrdinalIgnoreCase);

    public FileBrowserPanel()
    {
        InitializeComponent();
        _ready = true;
        TypeBox.SelectedIndex = 0;
        LoadPlaces();
    }

    /// <summary>打开面板并定位到某个目录（目录不存在就退回上次目录 / 主目录）。</summary>
    public void Show(string? startDir, bool dark, IEnumerable<string>? textExtensions = null)
    {
        _dark = dark;
        ApplyTheme(dark);

        if (textExtensions is not null)
        {
            _textExtensions.Clear();
            foreach (string ext in textExtensions)
            {
                if (!string.IsNullOrWhiteSpace(ext)) _textExtensions.Add(ext.TrimStart('.'));
            }
        }

        string dir = !string.IsNullOrWhiteSpace(startDir) && Directory.Exists(startDir)
            ? startDir
            : (_lastDir ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        _filter = string.Empty;
        FilterBox.Text = string.Empty;
        FileNameBox.Text = string.Empty;
        ShowHint(string.Empty);
        HideSuggest();
        Navigate(dir);

        Dispatcher.BeginInvoke(new Action(() => FileList.Focus()));
    }

    // ------------------------------------------------------------ 快捷位置

    private void LoadPlaces()
    {
        PlaceList.Items.Clear();

        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddPlace("主目录", profile);
        AddPlace("桌面", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddPlace("文档", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddPlace("下载", Path.Combine(profile, "Downloads"));

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "本地磁盘" : drive.VolumeLabel;
            AddPlace($"{label} ({drive.Name.TrimEnd('\\')})", drive.RootDirectory.FullName);
        }
    }

    private void AddPlace(string name, string path)
    {
        if (!Directory.Exists(path)) return;

        var item = new ListBoxItem
        {
            Content = new TextBlock
            {
                Text = name,
                FontSize = 12.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            Tag = path,
            Padding = new Thickness(10, 6, 10, 6),
            Style = (Style)FindResource("RecentItem"),
        };
        PlaceList.Items.Add(item);
    }

    private void PlaceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlaceList.SelectedItem is ListBoxItem item && item.Tag is string path) Navigate(path);
    }

    // ------------------------------------------------------------ 目录导航

    private void Navigate(string dir)
    {
        try
        {
            _dir = Path.GetFullPath(dir);
            _lastDir = _dir;
            HideSuggest();
            Refresh();

            // 路径长的时候默认看到的是开头；把光标挪到末尾，露出来的是最里层目录
            PathBox.CaretIndex = PathBox.Text.Length;
            PathBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            ShowInfoItem($"打不开这个目录：{ex.Message}");
        }
    }

    private void Refresh()
    {
        if (!_ready) return;

        FileList.Items.Clear();
        PathBox.Text = _dir;

        if (!Directory.Exists(_dir))
        {
            ShowInfoItem("目录不存在");
            return;
        }

        try
        {
            var dirs = new List<string>();
            var files = new List<string>();

            foreach (string path in Directory.EnumerateDirectories(_dir))
            {
                if (IsHidden(path)) continue;
                if (!MatchName(Path.GetFileName(path))) continue;
                dirs.Add(path);
            }

            foreach (string path in Directory.EnumerateFiles(_dir))
            {
                if (IsHidden(path)) continue;
                if (!Accept(Path.GetFileName(path))) continue;
                files.Add(path);
            }

            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string path in dirs) FileList.Items.Add(MakeEntry(path, true));
            foreach (string path in files) FileList.Items.Add(MakeEntry(path, false));

            if (FileList.Items.Count == 0)
            {
                ShowInfoItem(_filter.Length > 0
                    ? "没有匹配的文件（可以切到“所有文件”再试）"
                    : "这个目录里没有可显示的文件");
            }
        }
        catch (UnauthorizedAccessException)
        {
            ShowInfoItem("没有访问这个目录的权限");
        }
        catch (Exception ex)
        {
            ShowInfoItem($"读不出来：{ex.Message}");
        }
    }

    private bool Accept(string name)
    {
        if (!MatchName(name)) return false;
        if (!_onlyText || _textExtensions.Count == 0) return true;

        string ext = Path.GetExtension(name).TrimStart('.');
        return ext.Length == 0 || _textExtensions.Contains(ext);   // 没后缀的按文本处理，留着
    }

    /// <summary>名称过滤（目录与文件都吃这一条；扩展名过滤只作用于文件）。</summary>
    private bool MatchName(string name) =>
        _filter.Length == 0 || name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsHidden(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return (info.Attributes & FileAttributes.Hidden) != 0;
        }
        catch
        {
            return false;
        }
    }

    // ------------------------------------------------------------ 列表条目

    private ListBoxItem MakeEntry(string path, bool isDir)
    {
        var icon = new System.Windows.Shapes.Path
        {
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(2, 0, 10, 0),
            Stroke = Solid(isDir ? (_dark ? "#C7C7C7" : "#5A5A62") : (_dark ? "#9A9AA2" : "#8A8A8A")),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = Geometry.Parse(isDir ? FolderGlyph : FileGlyph),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var name = new TextBlock
        {
            Text = Path.GetFileName(path),
            FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Solid(_dark ? "#E6E6E6" : "#1F1F1F"),
            VerticalAlignment = VerticalAlignment.Center,
            // 名字被截短时，鼠标停上去能看到完整路径
            ToolTip = path,
        };

        var meta = new TextBlock
        {
            Text = isDir ? string.Empty : Describe(path),
            FontSize = 11,
            Opacity = 0.55,
            Foreground = Solid(_dark ? "#C7C7C7" : "#5A5A62"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 2, 0),
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(name, 1);
        Grid.SetColumn(meta, 2);
        row.Children.Add(icon);
        row.Children.Add(name);
        row.Children.Add(meta);

        var item = new ListBoxItem
        {
            Content = row,
            Tag = new Entry(path, isDir),
            Style = (Style)FindResource("RecentItem"),
        };
        item.MouseDoubleClick += (_, _) =>
        {
            PlaceList.SelectedItem = null;
            if (isDir) Navigate(path);
            else Open(path);
        };

        return item;
    }

    private void ShowInfoItem(string message)
    {
        FileList.Items.Add(new ListBoxItem
        {
            Content = new TextBlock
            {
                Text = message,
                FontSize = 12.5,
                Opacity = 0.6,
                Margin = new Thickness(8, 10, 8, 10),
                Foreground = Solid(_dark ? "#C7C7C7" : "#6E6E73"),
            },
            Style = (Style)FindResource("RecentItem"),
        });
    }

    private static string Describe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return $"{FormatSize(info.Length)}    {info.LastWriteTime:yyyy-MM-dd HH:mm}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.#} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.#} GB";
    }

    // ------------------------------------------------------------ 打开

    private void Open(string path)
    {
        if (Directory.Exists(path))
        {
            Navigate(path);
            return;
        }

        if (!File.Exists(path)) return;
        FileOpened?.Invoke(path);
    }

    private string? SelectedPath() =>
        FileList.SelectedItem is ListBoxItem item && item.Tag is Entry entry ? entry.Path : null;

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        string name = FileNameBox.Text.Trim();
        string? selected = SelectedPath();

        string? target = name.Length == 0 ? selected
            : Path.IsPathRooted(name) ? name
            : Path.Combine(_dir, name);

        if (string.IsNullOrEmpty(target)) return;
        Open(target);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is not ListBoxItem item || item.Tag is not Entry entry) return;
        if (entry.IsDir) return;
        FileNameBox.Text = Path.GetFileName(entry.Path);
    }

    private void FileList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        string? path = SelectedPath();
        if (path is null) return;
        Open(path);
        e.Handled = true;
    }

    private void FileNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        OpenButton_Click(sender, e);
        e.Handled = true;
    }

    // ------------------------------------------------------------ 路径：补全 / 跳转

    /// <summary>回车 / 点候选：进目录、打开文件，路径不对就给提示。</summary>
    private void CommitPath(string raw)
    {
        string text = raw.Trim();
        if (text.Length == 0) return;

        HideSuggest();

        try
        {
            // 支持 %TEMP% 这类环境变量
            string target = Environment.ExpandEnvironmentVariables(text);

            if (Directory.Exists(target))
            {
                ShowHint(string.Empty);
                Navigate(target);
                return;
            }

            if (File.Exists(target))
            {
                ShowHint(string.Empty);
                Open(target);
                return;
            }

            // 不是完整路径时，当成当前目录下的名字再试一次
            string combined = Path.Combine(_dir, target);
            if (Directory.Exists(combined)) { Navigate(combined); return; }
            if (File.Exists(combined)) { Open(combined); return; }

            ShowHint($"找不到这个路径：{target}");
        }
        catch (Exception ex)
        {
            ShowHint($"路径不对：{ex.Message}");
        }
    }

    private void ShowHint(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            PathHint.Visibility = Visibility.Collapsed;
            return;
        }

        PathHint.Text = message;
        PathHint.Visibility = Visibility.Visible;
    }

    /// <summary>输入过程中给出候选目录。</summary>
    private void UpdateSuggest()
    {
        string text = PathBox.Text.Trim();
        var items = Suggest(text);

        SuggestList.Items.Clear();
        foreach (string path in items)
        {
            SuggestList.Items.Add(new ListBoxItem
            {
                Content = new TextBlock
                {
                    Text = path,
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                Tag = path,
                Padding = new Thickness(10, 6, 10, 6),
            });
        }

        if (items.Count == 0)
        {
            HideSuggest();
            return;
        }

        PathPopup.IsOpen = true;
        SuggestList.SelectedIndex = 0;
        SuggestList.ScrollIntoView(SuggestList.Items[0]);

        // 弹层用 StaysOpen=True 避免外部按键把它关掉；但弹层打开时焦点可能短暂离开路径框，
        // 这里把焦点夺回路径框，保证回车/Tab 仍由路径框处理（接受或补全候选）。
        if (!PathBox.IsKeyboardFocused)
        {
            PathBox.Focus();
        }
    }

    private List<string> Suggest(string text)
    {
        var result = new List<string>();
        if (text.Length == 0) return result;

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(text);

            // 盘符：输入 C 或 C:\ 时给出盘符候选
            if (!expanded.Contains('\\') && !expanded.Contains('/'))
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (drive.Name.StartsWith(expanded, StringComparison.OrdinalIgnoreCase))
                        result.Add(drive.RootDirectory.FullName.TrimEnd('\\'));
                }
            }

            string parent;
            string prefix;

            if (Path.IsPathRooted(expanded))
            {
                int cut = expanded.LastIndexOfAny(new[] { '\\', '/' });
                if (cut >= 0)
                {
                    parent = expanded.Substring(0, cut + 1);
                    prefix = expanded.Substring(cut + 1);
                    if (parent.Length == 0) parent = expanded;
                }
                else
                {
                    parent = expanded + Path.DirectorySeparatorChar;
                    prefix = string.Empty;
                }
            }
            else
            {
                parent = _dir;
                prefix = expanded;
            }

            if (Directory.Exists(parent))
            {
                foreach (string dir in Directory.EnumerateDirectories(parent))
                {
                    if (IsHidden(dir)) continue;
                    if (prefix.Length > 0 &&
                        Path.GetFileName(dir).IndexOf(prefix, StringComparison.OrdinalIgnoreCase) != 0) continue;

                    result.Add(dir);
                    if (result.Count >= 30) break;
                }
            }
        }
        catch
        {
            // 补全只是辅助，出错就不给候选
        }

        return result;
    }

    private void HideSuggest() => PathPopup.IsOpen = false;

    private void PathBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool open = PathPopup.IsOpen && SuggestList.Items.Count > 0;

        switch (e.Key)
        {
            case Key.Down:
                if (open)
                {
                    SuggestList.SelectedIndex = Math.Min(SuggestList.Items.Count - 1, SuggestList.SelectedIndex + 1);
                    SuggestList.ScrollIntoView(SuggestList.SelectedItem);
                    e.Handled = true;
                }
                return;

            case Key.Up:
                if (open)
                {
                    SuggestList.SelectedIndex = Math.Max(0, SuggestList.SelectedIndex - 1);
                    SuggestList.ScrollIntoView(SuggestList.SelectedItem);
                    e.Handled = true;
                }
                return;

            case Key.Escape:
                if (open)
                {
                    HideSuggest();
                    e.Handled = true;
                }
                return;

            case Key.Tab:
                if (open && SuggestList.SelectedItem is ListBoxItem pick)
                {
                    ApplySuggest(pick.Tag as string ?? string.Empty);
                    e.Handled = true;
                }
                return;

            case Key.Enter:
                // 候选开着就先用候选，没有就按输入的内容跳
                if (open)
                {
                    var item = SuggestList.SelectedItem as ListBoxItem
                               ?? (SuggestList.Items.Count > 0 ? SuggestList.Items[0] as ListBoxItem : null);
                    if (item is not null)
                    {
                        ApplySuggest(item.Tag as string ?? string.Empty);
                        e.Handled = true;
                        return;
                    }
                }

                CommitPath(PathBox.Text);
                e.Handled = true;
                return;
        }
    }

    private void ApplySuggest(string path)
    {
        if (path.Length == 0) return;
        PathBox.Text = path;
        PathBox.CaretIndex = path.Length;
        PathBox.ScrollToEnd();
        HideSuggest();
        CommitPath(path);
    }

    private void PathBox_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Enter:
            case Key.Escape:
            case Key.Tab:
            case Key.Left:
            case Key.Right:
                return;
        }

        UpdateSuggest();
    }

    /// <summary>点路径框 / 弹层（弹层是独立 HWND，不会到这里）以外的地方收起补全弹层。
    /// 注意：不能靠 PathBox.LostFocus 收起——弹层打开时焦点会临时离开路径框，
    /// 那样会在打开的瞬间就被收掉。</summary>
    private void Card_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != PathBox) HideSuggest();
    }

    private void SuggestList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SuggestList.SelectedItem is ListBoxItem item)
        {
            ApplySuggest(item.Tag as string ?? string.Empty);
            e.Handled = true;
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filter = FilterBox.Text.Trim();
        Refresh();
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _onlyText = TypeBox.SelectedIndex == 0;
        Refresh();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DirectoryInfo? parent = Directory.GetParent(_dir.TrimEnd('\\'));
            if (parent is null) return;
            Navigate(parent.FullName);
        }
        catch
        {
            // 已经到根了
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Closed?.Invoke();

    // ------------------------------------------------------------ 主题

    public void ApplyTheme(bool dark)
    {
        _dark = dark;

        string card = dark ? "#2D2D2D" : "#FFFFFF";
        string border = dark ? "#3A3A3A" : "#E4E4E7";
        string text = dark ? "#E6E6E6" : "#1F1F1F";
        string dim = dark ? "#C7C7C7" : "#6E6E73";
        string field = dark ? "#1E1E1E" : "#FFFFFF";
        string fieldBorder = dark ? "#3A3A3A" : "#D8D8DC";
        string listBg = dark ? "#262626" : "#F7F7F9";
        string glyph = dark ? "#C7C7C7" : "#5A5A62";

        Card.Background = Solid(card);
        Card.BorderBrush = Solid(border);

        TitleText.Foreground = Solid(text);
        NameLabel.Foreground = Solid(dim);
        TitleIcon.Stroke = Solid(glyph);
        UpGlyph.Stroke = Solid(glyph);
        RefreshGlyph.Stroke = Solid(glyph);
        FilterIcon.Stroke = Solid(dark ? "#9A9AA2" : "#8A8A8A");

        foreach (var box in new[] { PathBox, FileNameBox, FilterBox })
        {
            box.Background = Solid(field);
            box.Foreground = Solid(text);
            box.BorderBrush = Solid(fieldBorder);
            box.CaretBrush = Solid(text);
        }

        var listBorder = (Border)FileList.Parent;
        listBorder.Background = Solid(listBg);
        listBorder.BorderBrush = Solid(border);

        TypeBox.Background = Solid(field);
        TypeBox.Foreground = Solid(text);
        TypeBox.BorderBrush = Solid(fieldBorder);

        CloseButton.Foreground = Solid(dim);
        CloseButton.BorderBrush = Solid(fieldBorder);
        CancelButton.Foreground = Solid(dim);
        CancelButton.BorderBrush = Solid(fieldBorder);
        PathHint.Foreground = Solid(dark ? "#FF8A8A" : "#C0392B");

        Refresh();
    }

    private static SolidColorBrush Solid(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));
}
