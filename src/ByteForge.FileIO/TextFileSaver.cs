// Copyright (c) 2026 ByteForge
using System.Text;

namespace ByteForge.FileIO;

/// <summary>保存文件：沿用打开时的编码 / 换行符，写临时文件再替换，避免写坏原文件。</summary>
public static class TextFileSaver
{
    public static void Save(string path, string text, Encoding encoding, string lineEnding, bool writeBom)
    {
        string normalized = NormalizeLineEndings(text, lineEnding);
        byte[] bytes = writeBom
            ? encoding.GetPreamble().Concat(encoding.GetBytes(normalized)).ToArray()
            : encoding.GetBytes(normalized);

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temp = path + ".bf-tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { /* 忽略清理失败 */ }
            }
        }
    }

    /// <summary>另存为。</summary>
    public static void SaveAs(string path, string text, string lineEnding = "\r\n") =>
        Save(path, text, new UTF8Encoding(true), lineEnding, writeBom: true);

    /// <summary>统一换行符（编辑区内部一律用 \r\n，保存时按目标格式转换）。</summary>
    public static string NormalizeLineEndings(string text, string lineEnding)
    {
        if (string.IsNullOrEmpty(lineEnding) || lineEnding == "\r\n") return text;
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", lineEnding);
    }
}
