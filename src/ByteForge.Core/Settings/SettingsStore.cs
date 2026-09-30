// Copyright (c) 2026 ByteForge
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteForge.Core.Settings;

/// <summary>设置读写：settings.json 存放在 AppPaths.SettingsFilePath（安装目录）。</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static AppSettings? _current;

    /// <summary>当前设置（首次访问时从磁盘加载，失败则用默认值）。</summary>
    public static AppSettings Current => _current ??= Load();

    public static string SettingsFilePath => AppPaths.SettingsFilePath;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // 配置损坏时不阻断启动，回到默认值。
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        _current = settings;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // 安装目录只读等情况下保存失败，不抛出影响主流程。
        }
    }

    public static void SaveCurrent() => Save(Current);
}
