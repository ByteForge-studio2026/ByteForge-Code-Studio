// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ByteForge.Core;
using ByteForge.Core.Legal;
using RadioButton = System.Windows.Controls.RadioButton;

namespace ByteForge.Setup;

public partial class MainWindow : Window
{
    private int _step = 1;
    private bool _installing;
    private bool _readToBottom;

    public MainWindow()
    {
        InitializeComponent();

        LoadIcon();
        AgreementTextBlock.Text = AgreementText.Text;
        BuildDriveList();
        if (string.IsNullOrEmpty(_targetDir)) SetTarget(InstallEngine.DefaultInstallDirectory);
        SubtitleText.Text = $"{AppInfo.Positioning} · {AppInfo.Version} · {AppInfo.Team}";
    }

    // ------------------------------------------------------------ 安装位置（选磁盘 -> 自动建 Code Studio 文件夹）

    private string _targetDir = string.Empty;

    /// <summary>盘符根（"C:\"）-> 对应的磁盘单选按钮，用来做"路径 ↔ 磁盘"实时双向同步。</summary>
    private readonly Dictionary<string, RadioButton> _driveButtons = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>防止"路径改了去勾磁盘、磁盘勾了又改路径"来回递归。</summary>
    private bool _syncingTarget;

    /// <summary>列出所有就绪的磁盘做单选，默认选系统盘。</summary>
    private void BuildDriveList()
    {
        DrivePanel.Children.Clear();
        _driveButtons.Clear();

        RadioButton? system = null;
        RadioButton? any = null;
        string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            string root = drive.RootDirectory.FullName;   // 例如 "C:\"
            var button = new RadioButton
            {
                Content = root.TrimEnd('\\'),
                GroupName = "installDrive",
                Margin = new Thickness(0, 0, 16, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            button.Checked += (_, _) => SetTarget(root);
            DrivePanel.Children.Add(button);
            _driveButtons[root] = button;

            any ??= button;
            if (string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase)) system = button;
        }

        if (system is not null) system.IsChecked = true;
        else if (any is not null) any.IsChecked = true;
    }

    /// <summary>设定安装目录；选的是磁盘根目录时自动补一个 Code Studio 文件夹。</summary>
    private void SetTarget(string dir)
    {
        if (_syncingTarget || string.IsNullOrWhiteSpace(dir)) return;

        try { dir = Path.GetFullPath(dir); }
        catch { return; }

        string root = Path.GetPathRoot(dir) ?? string.Empty;
        if (root.Length > 0 &&
            string.Equals(dir.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            dir = Path.Combine(root, InstallEngine.FolderName);
        }

        _targetDir = dir;
        TargetPathText.Text = dir;

        // 磁盘单选跟着路径走：路径落在哪个盘就选中哪个盘（自定义到别的盘时也一致）
        if (_driveButtons.TryGetValue(root, out RadioButton? button) && button.IsChecked != true)
        {
            _syncingTarget = true;
            try { button.IsChecked = true; }
            finally { _syncingTarget = false; }
        }
    }

    private void LoadIcon()
    {
        // 图标以 WPF 资源嵌在程序集里（单文件发布也不留散落文件）。
        // 优先用 1248x1248 的 PNG 按目标尺寸解码：ico 第一帧只有 16x16，放大发糊。
        BitmapImage? header = LoadResourceIcon("assets/icons/app-icon.png", 80);
        if (header is not null)
        {
            HeaderIcon.Source = header;
            TitleBarIcon.Source = LoadResourceIcon("assets/icons/app-icon.png", 40) ?? header;
            Icon = LoadResourceIcon("assets/icons/app-icon.png", 64) ?? header;
        }
        else
        {
            BitmapImage? ico = LoadResourceIcon("assets/icons/app-icon.ico", 0);
            if (ico is null) return;
            HeaderIcon.Source = ico;
            TitleBarIcon.Source = ico;
            Icon = ico;
        }
    }

    /// <summary>从程序集资源里取图标；decodeWidth &gt; 0 时按该宽度解码（避免大图硬缩发糊）。</summary>
    private static BitmapImage? LoadResourceIcon(string relativePath, int decodeWidth)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/" + relativePath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) image.DecodePixelWidth = decodeWidth;
            image.EndInit();
            return image;
        }
        catch
        {
            return null;
        }
    }

    // ------------------------------------------------------------ 自定义顶栏

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------ 第一步：协议

    private void AgreementScroller_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
        if (_readToBottom) return;

        bool atBottom = AgreementScroller.VerticalOffset + AgreementScroller.ViewportHeight
                        >= AgreementScroller.ExtentHeight - 4;

        if (!atBottom) return;

        _readToBottom = true;
        AgreeCheckBox.IsEnabled = true;
        ScrollHint.Text = "已阅读至底部，可勾选下方选项继续。";
        ScrollHint.Foreground = System.Windows.Media.Brushes.Green;
    }

    private void AgreeCheckBox_Changed(object sender, RoutedEventArgs e) =>
        NextButton.IsEnabled = AgreeCheckBox.IsChecked == true;

    // ------------------------------------------------------------ 步骤切换

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 1:
                ShowStep(2);
                break;
            case 2:
                _ = RunInstallAsync();
                break;
            case 3:
                LaunchAndClose();
                break;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(1);

    private void ShowStep(int step)
    {
        _step = step;

        StepAgreement.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepOptions.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepProgress.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step == 1 ? "下一步" : step == 2 ? "安装" : "启动";
        NextButton.IsEnabled = step != 3 || _installedPath is not null;
    }

    /// <summary>应用内目录选择器当前所在的目录。</summary>
    private string _folderCurrent = string.Empty;

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // 不用系统的 FolderBrowserDialog：在窗口里自己列目录挑安装位置
        string start = _targetDir;
        if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
            start = Path.GetPathRoot(_targetDir) ?? InstallEngine.DefaultInstallDirectory;
        ShowFolderPicker(start);
    }

    private void ShowFolderPicker(string dir)
    {
        _folderCurrent = dir;
        FolderPathBox.Text = dir;
        RefreshFolderList(dir);
        FolderPickerOverlay.Visibility = Visibility.Visible;
    }

    private void RefreshFolderList(string dir)
    {
        FolderListBox.Items.Clear();
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (string d in Directory.EnumerateDirectories(dir))
                {
                    var info = new DirectoryInfo(d);
                    if ((info.Attributes & FileAttributes.Hidden) != 0) continue;
                    FolderListBox.Items.Add(Path.GetFileName(d));
                }
            }
        }
        catch
        {
            // 读不出来就不列
        }

        if (FolderListBox.Items.Count == 0)
            FolderListBox.Items.Add("(此文件夹里没有子文件夹)");
    }

    private void FolderUpButton_Click(object sender, RoutedEventArgs e)
    {
        DirectoryInfo? parent = Directory.GetParent(_folderCurrent.TrimEnd('\\'));
        if (parent is null) return;
        _folderCurrent = parent.FullName;
        FolderPathBox.Text = _folderCurrent;
        RefreshFolderList(_folderCurrent);
    }

    private void FolderListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FolderListBox.SelectedItem is not string name || name.StartsWith("(")) return;
        string full = Path.Combine(_folderCurrent, name);
        if (Directory.Exists(full))
        {
            _folderCurrent = full;
            FolderPathBox.Text = full;
            RefreshFolderList(full);
        }
    }

    private void FolderPathBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        string text = FolderPathBox.Text.Trim();
        if (text.Length == 0) return;

        try
        {
            string full = Path.GetFullPath(text);
            if (Directory.Exists(full))
            {
                _folderCurrent = full;
            }
            else if (File.Exists(full))
            {
                DirectoryInfo? parent = Directory.GetParent(full);
                _folderCurrent = parent?.FullName ?? full;
            }
            else
            {
                _folderCurrent = full;
            }

            FolderPathBox.Text = _folderCurrent;
            RefreshFolderList(_folderCurrent);
        }
        catch
        {
            // 路径非法就忽略
        }

        e.Handled = true;
    }

    private void FolderNewButton_Click(object sender, RoutedEventArgs e)
    {
        string baseName = "新建文件夹";
        string name = baseName;
        int i = 1;
        while (Directory.Exists(Path.Combine(_folderCurrent, name))) name = $"{baseName} ({i++})";

        try
        {
            string created = Directory.CreateDirectory(Path.Combine(_folderCurrent, name)).FullName;
            _folderCurrent = created;
            FolderPathBox.Text = created;
            RefreshFolderList(created);
        }
        catch
        {
            // 建不了就忽略
        }
    }

    private void FolderPickerOk_Click(object sender, RoutedEventArgs e)
    {
        SetTarget(_folderCurrent);
        FolderPickerOverlay.Visibility = Visibility.Collapsed;
    }

    private void FolderPickerCancel_Click(object sender, RoutedEventArgs e) =>
        FolderPickerOverlay.Visibility = Visibility.Collapsed;

    private void FolderPickerClose_Click(object sender, RoutedEventArgs e) =>
        FolderPickerOverlay.Visibility = Visibility.Collapsed;

    private void FolderPickerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Grid) FolderPickerOverlay.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------ 安装

    private string? _installedPath;

    private async Task RunInstallAsync()
    {
        string target = _targetDir?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            System.Windows.MessageBox.Show(this, "请选择安装位置。", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _installing = true;
        NextButton.IsEnabled = false;
        BackButton.IsEnabled = false;
        ShowStep(3);

        var options = new InstallOptions
        {
            TargetDirectory = target,
            CreateStartMenuShortcut = ShortcutCheckBox.IsChecked == true,
            SetAsDefaultEditor = DefaultEditorCheckBox.IsChecked == true,
        };

        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                ProgressText.Text = p.Message;
                ProgressBar.Value = p.Percent;
            });

            await InstallEngine.InstallAsync(options, progress);

            _installedPath = target;
            FinishText.Text = $"已安装到：{target}\n\n" +
                              "提示：本软件为开源、公益性质的免费软件，按“现状”提供；" +
                              $"完整条款见{AppInfo.AgreementName}。";

            CancelButton.Content = "关闭";
            NextButton.Content = "启动";
            NextButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            FinishText.Text = $"安装失败：{ex.Message}";
            CancelButton.Content = "关闭";
            NextButton.IsEnabled = false;
        }
        finally
        {
            _installing = false;
        }
    }

    private void LaunchAndClose()
    {
        if (_installedPath is null) return;

        string exe = Path.Combine(_installedPath, AppInfo.ExecutableName);
        if (File.Exists(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch
            {
                // 启动失败就直接关闭
            }
        }
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            if (System.Windows.MessageBox.Show(this, "安装尚未完成，确定要取消吗？", "安装",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }
        Close();
    }
}
