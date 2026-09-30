// Copyright (c) 2026 ByteForge
using System.IO;
using System.IO.Compression;
using System.Reflection;
using ByteForge.Core;
using ByteForge.Core.Settings;
using ByteForge.FileIO;

namespace ByteForge.Setup;

/// <summary>安装选项。</summary>
public sealed class InstallOptions
{
    /// <summary>安装到哪个目录（选磁盘后自动拼上 Code Studio）。</summary>
    public required string TargetDirectory { get; init; }

    public bool CreateStartMenuShortcut { get; init; } = true;
    public bool SetAsDefaultEditor { get; init; } = true;
}

/// <summary>安装进度。</summary>
public readonly record struct InstallProgress(string Message, int Percent);

/// <summary>
/// 安装引擎：把内嵌的 payload.zip 解压到目标目录 → 写配置 → 建快捷方式 → 注册文件关联。
/// 全部只动当前用户可写的位置，不需要管理员权限。
/// </summary>
public static class InstallEngine
{
    /// <summary>在所选磁盘根下自动创建的文件夹名。</summary>
    public const string FolderName = "Code Studio";

    /// <summary>默认安装目录：系统盘下的 Code Studio。</summary>
    public static string DefaultInstallDirectory =>
        Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", FolderName);

    public static async Task InstallAsync(InstallOptions options, IProgress<InstallProgress> progress)
    {
        await Task.Run(() =>
        {
            progress.Report(new InstallProgress("正在创建安装目录…", 5));
            Directory.CreateDirectory(options.TargetDirectory);

            progress.Report(new InstallProgress("正在解压安装包…", 15));
            int extracted = ExtractPayload(options.TargetDirectory, progress);

            progress.Report(new InstallProgress("正在写入配置…", 80));
            EnsureSettings(options.TargetDirectory);

            if (options.CreateStartMenuShortcut)
            {
                progress.Report(new InstallProgress("正在创建开始菜单快捷方式…", 88));
                CreateStartMenuShortcut(options.TargetDirectory);
            }

            if (options.SetAsDefaultEditor)
            {
                progress.Report(new InstallProgress("正在关联文件类型…", 94));
                string exe = Path.Combine(options.TargetDirectory, AppInfo.ExecutableName);
                if (File.Exists(exe))
                {
                    FileAssociation.Register(exe, Path.Combine(options.TargetDirectory, "assets", "icons", "app-icon.ico"));
                }
            }

            progress.Report(new InstallProgress($"完成，共解压 {extracted} 个文件", 100));
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 把内嵌的 payload.zip 解压到目标目录。
    /// 找不到内嵌资源时（开发态直接跑 Setup），回落到旁边的 payload\ 目录直接拷贝。
    /// </summary>
    private static int ExtractPayload(string target, IProgress<InstallProgress> progress)
    {
        Assembly assembly = typeof(InstallEngine).Assembly;
        string? resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));

        if (resource is null) return CopyFromFolder(target);

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        string fullTarget = Path.GetFullPath(target);
        int total = Math.Max(1, zip.Entries.Count);
        int done = 0;

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            string destination = Path.GetFullPath(Path.Combine(fullTarget, entry.FullName));

            // 防目录穿越（zip slip）：解压路径必须落在目标目录内
            if (!destination.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase)) continue;

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            done++;

            if (done % 40 == 0 || done == total)
            {
                progress.Report(new InstallProgress($"正在解压 {done}/{total}", 15 + (int)(60.0 * done / total)));
            }
        }

        return done;
    }

    /// <summary>开发态回落：直接拷贝 Setup 旁边的 payload\ 目录。</summary>
    private static int CopyFromFolder(string target)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "payload");
        if (!Directory.Exists(source)) source = AppContext.BaseDirectory;

        int count = 0;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            if (name.StartsWith("ByteForgeSetup.", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Equals("payload.zip", StringComparison.OrdinalIgnoreCase)) continue;

            string destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
            count++;
        }

        return count;
    }

    private static void EnsureSettings(string targetDirectory)
    {
        // 安装目录可写时，settings.json 直接放到安装目录（便携模式）
        string settingsPath = Path.Combine(targetDirectory, "settings.json");
        if (File.Exists(settingsPath)) return;

        try
        {
            File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(
                new AppSettings(),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 写不进去也没关系，程序启动时会自己回落到 %LOCALAPPDATA%
        }
    }

    private static void CreateStartMenuShortcut(string targetDirectory)
    {
        try
        {
            string exe = Path.Combine(targetDirectory, AppInfo.ExecutableName);
            if (!File.Exists(exe)) return;

            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "ByteForge");
            Directory.CreateDirectory(startMenu);

            string iconPath = Path.Combine(targetDirectory, "assets", "icons", "app-icon.ico");
            string linkPath = Path.Combine(startMenu, "ByteForge Code Studio.lnk");
            if (File.Exists(linkPath)) File.Delete(linkPath);

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = exe;
            shortcut.WorkingDirectory = targetDirectory;
            shortcut.Description = "ByteForge Code Studio";
            if (File.Exists(iconPath)) shortcut.IconLocation = iconPath + ",0";
            shortcut.Save();
        }
        catch
        {
            // 快捷方式建不出来不影响使用
        }
    }
}
