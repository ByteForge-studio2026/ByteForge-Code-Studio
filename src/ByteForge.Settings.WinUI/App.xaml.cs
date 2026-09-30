// Copyright (c) 2026 ByteForge
using System.Diagnostics;
using System.IO;
using ByteForge.Core;
using ByteForge.Core.Settings;
using Microsoft.UI.Xaml;

namespace ByteForge.Settings.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        Diagnostics.Log("App ctor enter");
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Diagnostics.Log("UNHANDLED: " + e.ExceptionObject);
            UnhandledException += (_, e) => Diagnostics.Log("APP UNHANDLED: " + e.Exception);
        }
        catch (Exception ex)
        {
            Diagnostics.LogException("hook", ex);
        }

        try
        {
            InitializeComponent();
            Diagnostics.Log("App InitializeComponent OK");
        }
        catch (Exception ex)
        {
            Diagnostics.LogException("App.InitializeComponent", ex);
            throw;
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Diagnostics.Log("OnLaunched enter");

        // 主程序 exe（ByteForgeSettings.exe）是统一入口：
        // - 直接双击自己 → 打开设置窗口
        // - 双击已关联的 .txt/.py 等文件 → 唤起编辑器打开该文件，本进程退出
        string? fileArg = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(fileArg) && Path.GetExtension(fileArg).Length > 1)
        {
            Diagnostics.Log($"File argument: {fileArg}");
            OpenFileWithEditor(fileArg);

            // 已经把文件交给编辑器了，本进程不需要窗口，直接退出
            try { Exit(); } catch { /* 退不掉由系统回收 */ }
            return;
        }

        try
        {
            // 让设置窗口的主题与编辑器保持一致（都读同一份 AppSettings.Theme）
            ApplyThemeFromSettings();

            _window = new MainWindow();
            Diagnostics.Log("MainWindow created");
            _window.Activate();
            Diagnostics.Log("Activate OK");
        }
        catch (Exception ex)
        {
            Diagnostics.LogException("OnLaunched", ex);
            throw;
        }
    }

    /// <summary>
    /// 让设置窗口的主题（浅 / 深）与编辑器保持一致：都读同一份 AppSettings.Theme。
    /// 这样不会出现设置是浅色、编辑器是深色（或反过来）的割裂。
    /// </summary>
    public void ApplyThemeFromSettings()
    {
        string resolved = SettingsStore.Current.ResolvedTheme();
        RequestedTheme = string.Equals(resolved, "dark", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }

    private static void OpenFileWithEditor(string filePath)
    {
        string editor = Path.Combine(AppPaths.InstallDirectory, AppInfo.ExecutableName);
        if (!File.Exists(editor))
        {
            editor = Path.Combine(AppContext.BaseDirectory.TrimEnd('\\'), AppInfo.ExecutableName);
        }

        if (!File.Exists(editor))
        {
            Diagnostics.Log("Editor not found: " + editor);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(editor, $"\"{filePath}\"")
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? string.Empty,
            });
        }
        catch (Exception ex)
        {
            Diagnostics.LogException("OpenFileWithEditor", ex);
        }
    }
}
