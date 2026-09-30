// Copyright (c) 2026 ByteForge
using System.Reflection;

namespace ByteForge.Core.Legal;

/// <summary>用户协议文本。来源：程序集内嵌资源（docs 目录同名文件的副本）。</summary>
public static class AgreementText
{
    private const string ResourceName = "ByteForge.Core.Legal.Agreement.txt";

    private static string? _text;

    /// <summary>《ByteForge 产品用户协议 - 2026 年修订版》全文。</summary>
    public static string Text => _text ??= Load();

    private static string Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return $"{AppInfo.AgreementName}（资源缺失，请重新安装或从官方渠道获取）";
        }
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
