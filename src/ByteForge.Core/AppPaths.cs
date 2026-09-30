// Copyright (c) 2026 ByteForge
namespace ByteForge.Core;

/// <summary>
/// 动态路径：所有路径都以"安装目录"为根，不写死盘符。
/// 安装目录取当前 exe 所在目录（AppContext.BaseDirectory）。
/// 配置默认保存在安装目录（便携式）；目录不可写时自动回落到
/// %LOCALAPPDATA%\ByteForge\CodeStudio，避免程序启动失败。
/// </summary>
public static class AppPaths
{
    /// <summary>安装目录（主程序 / 设置程序 / Setup 共用同一逻辑）。</summary>
    public static string InstallDirectory { get; } = Normalize(AppContext.BaseDirectory);

    /// <summary>回落目录（安装目录只读时才用）。</summary>
    public static string FallbackDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ByteForge", "CodeStudio");

    /// <summary>settings.json 实际所在目录。</summary>
    public static string DataDirectory { get; private set; } = InstallDirectory;

    /// <summary>配置文件完整路径。</summary>
    public static string SettingsFilePath => Path.Combine(DataDirectory, "settings.json");

    /// <summary>图标文件路径（assets/icons/app-icon.ico）。</summary>
    public static string IconPath => Path.Combine(InstallDirectory, "assets", "icons", "app-icon.ico");

    /// <summary>
    /// 软件内部显示用的图标（assets/icons/app-icon.png，1248x1248）。
    /// ico 的第一帧只有 16x16，WinUI/WPF 直接解码会取到小帧再放大 → 发虚，
    /// 所以界面上一律用这张 PNG 配合 DecodePixelWidth 按目标尺寸解码。
    /// </summary>
    public static string IconPngPath => Path.Combine(InstallDirectory, "assets", "icons", "app-icon.png");

    /// <summary>软件内部显示用图标：PNG 存在就用 PNG，否则回落到 ico。</summary>
    public static string DisplayIconPath => File.Exists(IconPngPath) ? IconPngPath : IconPath;

    /// <summary>图标所在目录（assets/icons）。</summary>
    public static string IconDirectory => Path.Combine(InstallDirectory, "assets", "icons");

    /// <summary>日志目录。</summary>
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>安装目录里是否已经能写配置（true = 便携模式生效）。</summary>
    public static bool UsesInstallDirectory { get; private set; } = true;

    static AppPaths() => ResolveDataDirectory();

    /// <summary>确定配置目录：优先安装目录，写不进就用回落目录。</summary>
    private static void ResolveDataDirectory()
    {
        if (CanWrite(InstallDirectory))
        {
            DataDirectory = InstallDirectory;
            UsesInstallDirectory = true;
            return;
        }

        try
        {
            Directory.CreateDirectory(FallbackDataDirectory);
            DataDirectory = FallbackDataDirectory;
            UsesInstallDirectory = false;
        }
        catch
        {
            // 连回落目录都建不出来时，保持安装目录，后续写入自行失败。
            DataDirectory = InstallDirectory;
            UsesInstallDirectory = true;
        }
    }

    private static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string Normalize(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path[..^1]
            : path;
}
