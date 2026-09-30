// Copyright (c) 2026 ByteForge
using Microsoft.Win32;

namespace ByteForge.FileIO;

/// <summary>
/// 默认编辑器关联。全部写在 HKCU（当前用户），不需要管理员权限。
/// 说明：Windows 10+ 的"默认应用"由 UserChoice 哈希保护，无法静默改写；
/// 这里做的是标准做法——注册 ProgID 并把它设为该扩展名的默认处理程序。
/// 若系统已存在 UserChoice，则本程序会出现在"打开方式"列表里，用户点一次即可切为默认。
/// </summary>
public static class FileAssociation
{
    public const string ProgId = "ByteForgeCodeStudio";

    private const string ClassesRoot = @"Software\Classes";

    /// <summary>全部已知后缀（源码 + 纯文本）。</summary>
    public static IEnumerable<string> AllExtensions =>
        Core.AppInfo.SourceExtensions.Concat(Core.AppInfo.PlainTextExtensions).Distinct();

    /// <summary>注册为默认编辑器（关联全部已知后缀）。</summary>
    public static bool Register(string executablePath, string? iconPath = null) =>
        Register(executablePath, iconPath, AllExtensions);

    /// <summary>注册为默认编辑器，只关联指定的后缀。</summary>
    public static bool Register(string executablePath, string? iconPath, IEnumerable<string> extensions)
    {
        List<string> targets = Normalize(extensions);
        if (targets.Count == 0) return true;

        try
        {
            using RegistryKey classes = Registry.CurrentUser.CreateSubKey(ClassesRoot);
            WriteProgId(classes, executablePath, iconPath, targets);
            WriteApplicationsEntry(classes, executablePath, targets);

            foreach (string ext in targets)
            {
                using RegistryKey extKey = classes.CreateSubKey($".{ext}");
                extKey.SetValue(null, ProgId);

                using RegistryKey progIds = extKey.CreateSubKey("OpenWithProgids");
                progIds.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.Binary);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>取消全部关联（卸载时调用）。</summary>
    public static bool Unregister() => Unregister(AllExtensions);

    /// <summary>只取消指定后缀的关联。</summary>
    public static bool Unregister(IEnumerable<string> extensions)
    {
        List<string> targets = Normalize(extensions);
        if (targets.Count == 0) return true;

        try
        {
            using RegistryKey classes = Registry.CurrentUser.CreateSubKey(ClassesRoot);

            foreach (string ext in targets)
            {
                RemoveExtension(classes, ext);
            }

            // 一个后缀都没剩下时，把程序条目一并清掉
            if (!AllExtensions.Any(ext => IsRegistered(ext)))
            {
                classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
                classes.DeleteSubKeyTree($@"Applications\{Core.AppInfo.ExecutableName}", throwOnMissingSubKey: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>把关联设置成"恰好是这些后缀"：多余的取消，缺少的补上。</summary>
    public static bool Apply(string executablePath, string? iconPath, IEnumerable<string> extensions)
    {
        List<string> targets = Normalize(extensions);

        foreach (string ext in AllExtensions)
        {
            bool should = targets.Contains(ext);
            if (should == IsRegistered(ext)) continue;

            if (should)
            {
                Register(executablePath, iconPath, new[] { ext });
            }
            else
            {
                Unregister(new[] { ext });
            }
        }

        return true;
    }

    /// <summary>是否已经注册为默认编辑器（默认看 .py）。</summary>
    public static bool IsRegistered() => IsRegistered("py");

    /// <summary>某个后缀是否已经指向本程序。</summary>
    public static bool IsRegistered(string extension)
    {
        try
        {
            string ext = extension.TrimStart('.');
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"{ClassesRoot}\.{ext}");
            return string.Equals(key?.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- 内部

    private static void WriteProgId(RegistryKey classes, string executablePath, string? iconPath, List<string> targets)
    {
        using RegistryKey progId = classes.CreateSubKey(ProgId);
        progId.SetValue(null, "ByteForge Code Studio 源文件");

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            using RegistryKey icon = progId.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{iconPath}\",0");
        }

        using RegistryKey open = progId.CreateSubKey(@"shell\open\command");
        open.SetValue(null, $"\"{executablePath}\" \"%1\"");

        using RegistryKey supported = progId.CreateSubKey("SupportedTypes");
        foreach (string ext in targets)
        {
            supported.SetValue($".{ext}", string.Empty, RegistryValueKind.String);
        }
    }

    private static void WriteApplicationsEntry(RegistryKey classes, string executablePath, List<string> targets)
    {
        using RegistryKey app = classes.CreateSubKey($@"Applications\{Path.GetFileName(executablePath)}");
        app.SetValue("FriendlyAppName", "ByteForge Code Studio");

        using RegistryKey command = app.CreateSubKey(@"shell\open\command");
        command.SetValue(null, $"\"{executablePath}\" \"%1\"");

        using RegistryKey supported = app.CreateSubKey("SupportedTypes");
        foreach (string ext in targets)
        {
            supported.SetValue($".{ext}", string.Empty, RegistryValueKind.String);
        }
    }

    private static void RemoveExtension(RegistryKey classes, string extension)
    {
        string ext = extension.TrimStart('.');

        using (RegistryKey? extKey = classes.OpenSubKey($".{ext}", writable: true))
        {
            if (extKey is not null)
            {
                extKey.DeleteSubKeyTree("OpenWithProgids", throwOnMissingSubKey: false);
                if (string.Equals(extKey.GetValue(null) as string, ProgId, StringComparison.OrdinalIgnoreCase))
                {
                    // 空字符串代表"(默认)"值
                    extKey.DeleteValue(string.Empty, throwOnMissingValue: false);
                }
            }
        }

        // ProgID 的 SupportedTypes 里也去掉这一条
        using (RegistryKey? progId = classes.OpenSubKey(ProgId, writable: true))
        {
            if (progId is null) return;

            using RegistryKey? supported = progId.OpenSubKey("SupportedTypes", writable: true);
            supported?.DeleteValue($".{ext}", throwOnMissingValue: false);
        }
    }

    private static List<string> Normalize(IEnumerable<string> extensions) =>
        extensions
            .Select(e => e.Trim().TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();
}
