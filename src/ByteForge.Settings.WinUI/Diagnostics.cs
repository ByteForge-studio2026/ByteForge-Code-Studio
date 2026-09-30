// Copyright (c) 2026 ByteForge
namespace ByteForge.Settings.WinUI;

/// <summary>
/// 启动诊断日志。用于定位非打包 WinUI 3 程序启动崩溃，正式发布后可直接移除。
/// </summary>
internal static class Diagnostics
{
    private static readonly string LogFile = Path.Combine(Path.GetTempPath(), "bf-winui-startup.log");

    public static string LogPath => LogFile;

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss.fff} [{Environment.ProcessId}] {message}\n");
        }
        catch
        {
            // 日志失败不能影响主流程
        }
    }

    public static void LogException(string where, Exception ex)
    {
        Log($"{where} EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
        Log("--- stack ---");
        Log(ex.ToString());
    }
}
