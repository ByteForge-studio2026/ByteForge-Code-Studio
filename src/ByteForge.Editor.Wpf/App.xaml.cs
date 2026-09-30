// Copyright (c) 2026 ByteForge
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ByteForge.Core;

namespace ByteForge.Editor.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 双击文件关联启动之类的场景看不到异常窗口，把崩溃写进日志，方便事后查
        DispatcherUnhandledException += (_, args) =>
        {
            WriteLog("未处理异常（Dispatcher）", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteLog("未处理异常", args.ExceptionObject as Exception);

        try
        {
            // 双击已关联的文件 / 把文件拖到图标上时，第一个参数就是文件路径
            string? filePath = e.Args.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

            var window = new MainWindow(filePath);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            WriteLog("启动失败", ex);
            throw;
        }
    }

    private static void WriteLog(string title, Exception? ex)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "bf-wpf-startup.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 日志也写不了就算了，不能因为记日志再崩一次
        }
    }
}
