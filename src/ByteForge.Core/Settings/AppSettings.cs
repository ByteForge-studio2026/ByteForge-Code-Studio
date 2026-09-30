// Copyright (c) 2026 ByteForge
namespace ByteForge.Core.Settings;

/// <summary>
/// 应用设置，保存为 settings.json。
/// 这是"高度自定义"编辑器的全部可调项：外观、编辑器行为、缩进与换行、
/// 编码与保存、Markdown 预览、性能、启动与窗口、文件关联。
/// </summary>
public sealed class AppSettings
{
    // ---------------------------------------------------------------- 外观

    /// <summary>主题：light / dark / system。</summary>
    public string Theme { get; set; } = "light";

    public string FontFamily { get; set; } = "Consolas";

    public double FontSize { get; set; } = 14;

    // ---------------------------------------------------------------- 编辑器

    public bool ShowLineNumbers { get; set; } = true;

    public bool WordWrap { get; set; }

    /// <summary>高亮光标所在行。</summary>
    public bool HighlightCurrentLine { get; set; } = true;

    /// <summary>回车时沿用上一行的缩进。</summary>
    public bool AutoIndent { get; set; } = true;

    /// <summary>输入括号 / 引号时自动补另一半。</summary>
    public bool AutoCloseBrackets { get; set; } = true;

    public bool ShowStatusBar { get; set; } = true;

    // ---------------------------------------------------------------- 缩进与换行

    public int TabSize { get; set; } = 4;

    public bool TabToSpaces { get; set; } = true;

    /// <summary>保存时使用的换行符：crlf / lf / keep（保持文件原样）。</summary>
    public string LineEnding { get; set; } = "crlf";

    public bool TrimTrailingWhitespaceOnSave { get; set; }

    public bool InsertFinalNewlineOnSave { get; set; }

    // ---------------------------------------------------------------- 编码与保存

    /// <summary>新文件的默认编码：utf8 / utf8-bom。已打开的文件沿用它自己的编码。</summary>
    public string EncodingPreference { get; set; } = "utf8";

    // ---------------------------------------------------------------- Markdown

    /// <summary>预览位置：off / bottom / side。</summary>
    public string MarkdownPreviewMode { get; set; } = "bottom";

    // ---------------------------------------------------------------- 性能

    /// <summary>超过这个体积就关闭语法高亮（KB），0 表示不限制。</summary>
    public int HighlightMaxFileSizeKb { get; set; } = 512;

    // ---------------------------------------------------------------- 启动与窗口

    public bool OpenLastFileOnStart { get; set; }

    /// <summary>最近文件列表保留条数。</summary>
    public int MaxRecentFiles { get; set; } = 10;

    public bool RememberWindowSize { get; set; } = true;

    public List<string> RecentFiles { get; set; } = new();

    /// <summary>每个最近文件"最后一次被打开"的时间，用于侧栏按时间分组。旧配置里没有，回退到文件修改时间。</summary>
    public Dictionary<string, DateTime> RecentFileTimes { get; set; } = new();

    /// <summary>用户自建的分组名（按显示顺序）。</summary>
    public List<string> RecentGroups { get; set; } = new();

    /// <summary>文件被放进哪个自建分组：path → 组名。没有记录的走"按时间自动分组"。</summary>
    public Dictionary<string, string> RecentFileGroups { get; set; } = new();

    /// <summary>被折叠的分组名。</summary>
    public List<string> CollapsedGroups { get; set; } = new();

    /// <summary>置顶的分组名（置顶的组排在列表最前面）。</summary>
    public List<string> PinnedGroups { get; set; } = new();

    /// <summary>置顶的文件（置顶的文件单独列在最上面的"置顶"区）。</summary>
    public List<string> PinnedRecentFiles { get; set; } = new();

    /// <summary>上次打开的文件（配合 OpenLastFileOnStart）。</summary>
    public string LastOpenFile { get; set; } = string.Empty;

    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 720;
    public bool WindowMaximized { get; set; }

    // ---------------------------------------------------------------- 文件关联

    public bool IsDefaultEditor { get; set; }

    /// <summary>要关联的扩展名（不含点）。空集合表示不关联任何类型。</summary>
    public List<string> AssociatedExtensions { get; set; } = new(DefaultExtensions());

    /// <summary>默认关联的全部已知后缀。</summary>
    public static List<string> DefaultExtensions() =>
        AppInfo.SourceExtensions.Concat(AppInfo.PlainTextExtensions).Distinct().ToList();

    // ---------------------------------------------------------------- 辅助

    /// <summary>主题实际取值（把 system 解析成 light / dark）。</summary>
    public string ResolvedTheme()
    {
        if (!string.Equals(Theme, "system", StringComparison.OrdinalIgnoreCase)) return Theme;
        return SystemThemeIsDark() ? "dark" : "light";
    }

    /// <summary>读取系统的应用主题（Windows 设置 → 个性化 → 颜色）。</summary>
    private static bool SystemThemeIsDark()
    {
        try
        {
            object? value = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>添加最近文件（去重、置顶、限量）。</summary>
    public void PushRecentFile(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        RecentFileTimes[path] = DateTime.Now;

        int limit = Math.Max(0, MaxRecentFiles);
        if (RecentFiles.Count > limit)
        {
            RecentFiles.RemoveRange(limit, RecentFiles.Count - limit);
        }

        // 顺手把已经不在列表里的记录清掉，避免 settings.json 越攒越大
        var keep = new HashSet<string>(RecentFiles, StringComparer.OrdinalIgnoreCase);
        foreach (string key in RecentFileTimes.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            RecentFileTimes.Remove(key);
        }
    }

    /// <summary>移除某个最近文件（侧栏条目上的删除按钮）。</summary>
    public void RemoveRecentFile(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFileTimes.Remove(path);
        RecentFileGroups.Remove(path);
        PinnedRecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>最后一次打开该文件的时间；没有记录就退回文件的修改时间。</summary>
    public DateTime RecentTimeOf(string path)
    {
        if (RecentFileTimes.TryGetValue(path, out DateTime t)) return t;
        try { return File.GetLastWriteTime(path); }
        catch { return DateTime.MinValue; }
    }

    // ---------------------------------------------------------------- 分组

    /// <summary>文件所属的自建分组；没分组返回空串（走按时间自动分组）。</summary>
    public string GroupOf(string path) =>
        RecentFileGroups.TryGetValue(path, out string? g) ? g : string.Empty;

    /// <summary>新建分组（同名忽略）。</summary>
    public void AddRecentGroup(string name)
    {
        string n = (name ?? string.Empty).Trim();
        if (n.Length == 0) return;
        if (!RecentGroups.Any(g => string.Equals(g, n, StringComparison.OrdinalIgnoreCase))) RecentGroups.Add(n);
    }

    /// <summary>重命名分组，组内的文件与折叠状态一起跟着改。</summary>
    public void RenameRecentGroup(string oldName, string newName)
    {
        string n = (newName ?? string.Empty).Trim();
        if (n.Length == 0) return;

        int i = RecentGroups.FindIndex(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase));
        if (i < 0) return;
        RecentGroups[i] = n;

        foreach (string path in RecentFileGroups
                 .Where(kv => string.Equals(kv.Value, oldName, StringComparison.OrdinalIgnoreCase))
                 .Select(kv => kv.Key).ToList())
        {
            RecentFileGroups[path] = n;
        }

        int c = CollapsedGroups.FindIndex(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase));
        if (c >= 0) CollapsedGroups[c] = n;

        int pi = PinnedGroups.FindIndex(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase));
        if (pi >= 0) PinnedGroups[pi] = n;
    }

    /// <summary>删除分组：只解散分组，文件本身与"最近打开"记录都保留，回到按时间自动分组。</summary>
    public void RemoveRecentGroup(string name)
    {
        RecentGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        CollapsedGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        PinnedGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));

        foreach (string path in RecentFileGroups
                 .Where(kv => string.Equals(kv.Value, name, StringComparison.OrdinalIgnoreCase))
                 .Select(kv => kv.Key).ToList())
        {
            RecentFileGroups.Remove(path);
        }
    }

    /// <summary>把文件放进分组；传 null / 空表示移出分组（回到按时间自动分组）。</summary>
    public void SetRecentFileGroup(string path, string? group)
    {
        if (string.IsNullOrWhiteSpace(group)) RecentFileGroups.Remove(path);
        else RecentFileGroups[path] = group.Trim();
    }

    public bool IsGroupCollapsed(string name) =>
        CollapsedGroups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));

    public void ToggleGroupCollapsed(string name)
    {
        if (IsGroupCollapsed(name))
            CollapsedGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        else
            CollapsedGroups.Add(name);
    }

    // ---------------------------------------------------------------- 置顶

    public bool IsGroupPinned(string name) =>
        PinnedGroups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));

    public bool IsFilePinned(string path) =>
        PinnedRecentFiles.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    /// <summary>切换分组置顶：置顶的组排在最前，取消后回到建组顺序。</summary>
    public void ToggleGroupPinned(string name)
    {
        if (IsGroupPinned(name))
            PinnedGroups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        else
            PinnedGroups.Add(name);
    }

    /// <summary>切换文件置顶：置顶的文件进最上面的"置顶"区，取消后回到原分组 / 时间分组。</summary>
    public void ToggleFilePinned(string path)
    {
        if (IsFilePinned(path))
            PinnedRecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        else
            PinnedRecentFiles.Add(path);
    }
}
