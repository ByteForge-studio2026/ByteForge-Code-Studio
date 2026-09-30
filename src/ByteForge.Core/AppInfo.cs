// Copyright (c) 2026 ByteForge
namespace ByteForge.Core;

/// <summary>应用元信息：关于页、设置页、Setup 都从这里取，避免各处写死。</summary>
public static class AppInfo
{
    public const string Name = "ByteForge Code Studio";
    public const string Version = "v0.1.0-alpha.1";
    public const string Positioning = "轻量级文本 / 源码编辑器";
    public const string TechStack = "C# + WPF（单文件应用，设置已内置）";
    public const string Team = "ByteForge 工作室";
    public const string License = "MIT License";
    public const string LicenseUrl = "https://opensource.org/licenses/MIT";
    public const string AgreementName = "《ByteForge 产品用户协议 - 2026 年修订版》";
    public const string Copyright = "Copyright (c) 2026 ByteForge";
    public const string OfficialChannel = "抖音号 ByteForge2026（ByteForge 官方）";
    public const string GitHubRepo = "GitHub：ByteForge-OpenSource";

    public const string ExecutableName = "ByteForgeCodeStudio.exe";
    public const string SettingsExecutableName = "ByteForgeSettings.exe";
    public const string SetupExecutableName = "ByteForgeSetup.exe";

    public const string FeatureSummary =
        "打开文件、编辑文本、保存文件、语法高亮（Python / C / C++ / Java / C# / Kotlin / PHP / XML / XAML / Markdown）、Markdown 预览";

    /// <summary>受支持的源码后缀（不含点）。</summary>
    public static readonly string[] SourceExtensions =
    {
        "py", "c", "h", "cpp", "hpp", "java", "cs", "xaml", "kt", "xml", "php", "md",
    };

    /// <summary>纯文本后缀（无语法高亮）。</summary>
    public static readonly string[] PlainTextExtensions =
    {
        "txt", "log", "ini", "cfg", "json", "yml", "yaml", "toml", "props", "gitignore",
    };

    /// <summary>所有无后缀文件按文本打开（含 NUL 字节时提示不受支持）。</summary>
    public static bool IsKnownExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return SourceExtensions.Contains(ext) || PlainTextExtensions.Contains(ext);
    }

    /// <summary>设置程序 exe 的完整路径（安装目录下，动态推导）。</summary>
    public static string SettingsExecutablePath =>
        Path.Combine(AppPaths.InstallDirectory, SettingsExecutableName);
}

