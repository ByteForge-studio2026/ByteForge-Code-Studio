// Copyright (c) 2026 ByteForge
using System.IO;
using System.Windows;

namespace ByteForge.Setup;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 静默安装（可选）：ByteForgeSetup.exe --install "D:\Code Studio" [--no-shortcut] [--no-assoc]
        // 不带参数就是正常的图形安装界面。
        if (e.Args.Any(a => a.Equals("--install", StringComparison.OrdinalIgnoreCase)))
        {
            RunSilentInstall(e.Args);
            return;
        }

        new MainWindow().Show();
    }

    private static void RunSilentInstall(string[] args)
    {
        int index = Array.FindIndex(args, a => a.Equals("--install", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return;

        string target = args[index + 1];
        var options = new InstallOptions
        {
            TargetDirectory = target,
            CreateStartMenuShortcut = !args.Any(a => a.Equals("--no-shortcut", StringComparison.OrdinalIgnoreCase)),
            SetAsDefaultEditor = !args.Any(a => a.Equals("--no-assoc", StringComparison.OrdinalIgnoreCase)),
        };

        // WinExe 没有控制台，把进度/结果写到日志方便排查
        string log = Path.Combine(Path.GetTempPath(), "bf-setup-silent.log");
        int exitCode = 0;
        try
        {
            File.WriteAllText(log, $"target={target}{Environment.NewLine}");
            InstallEngine.InstallAsync(options, new DirectProgress(log)).GetAwaiter().GetResult();
            File.AppendAllText(log, "OK" + Environment.NewLine);
        }
        catch (Exception ex)
        {
            File.AppendAllText(log, "FAIL " + ex + Environment.NewLine);
            exitCode = 1;
        }

        // 无界面的静默安装：装完直接退出（Shutdown 在 OnStartup 里可能不生效）
        Environment.Exit(exitCode);
    }

    /// <summary>把进度直接写日志（不经过会被挡住的 UI 线程）。</summary>
    private sealed class DirectProgress : IProgress<InstallProgress>
    {
        private readonly string _log;
        public DirectProgress(string log) => _log = log;

        public void Report(InstallProgress value)
        {
            try { File.AppendAllText(_log, $"{value.Percent}% {value.Message}{Environment.NewLine}"); }
            catch { /* 写日志失败不影响安装 */ }
        }
    }
}
