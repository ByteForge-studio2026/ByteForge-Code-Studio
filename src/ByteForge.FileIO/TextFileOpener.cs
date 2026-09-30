// Copyright (c) 2026 ByteForge
using System.Text;

namespace ByteForge.FileIO;

/// <summary>
/// 打开文件模块：
/// 1) 无后缀文件按文本处理；
/// 2) 含 NUL 字节等二进制特征 → 抛 UnsupportedBinaryFileException（提示"该文件是不受支持的二进制文件"）；
/// 3) 探测编码与换行符，交回纯文本供编辑器加载。
/// 本模块不引用任何 UI 框架，也不依赖语法高亮模块。
/// </summary>
public static class TextFileOpener
{
    /// <summary>二进制特征扫描窗口。</summary>
    public const int BinaryScanBytes = 64 * 1024;

    /// <summary>超过此大小按"大文件"处理（仍打开，但编辑器不启用高亮）。</summary>
    public const long LargeFileBytes = 2L * 1024 * 1024;

    /// <summary>硬上限：超过直接拒绝，避免把内存吃满。</summary>
    public const long MaxOpenBytes = 64L * 1024 * 1024;

    /// <summary>打开文本文件。</summary>
    public static OpenResult Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new FileOpenException(path ?? "", "路径为空。");

        if (!File.Exists(path))
            throw new FileOpenException(path, "文件不存在。");

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxOpenBytes)
                throw new FileOpenException(path,
                    $"文件过大（{info.Length / 1024 / 1024} MB），超过 {MaxOpenBytes / 1024 / 1024} MB 上限。");

            byte[] bytes = File.ReadAllBytes(path);

            // 二进制判定：扫描前 BinaryScanBytes，出现 NUL 即视为二进制
            if (LooksLikeBinary(bytes))
                throw new UnsupportedBinaryFileException(path);

            var (encoding, hasBom) = EncodingDetector.Detect(bytes, bytes.Length);
            string text = encoding.GetString(bytes);

            // 去掉 BOM 残留（UTF8 无 BOM 解码时会留下 U+FEFF）
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];

            string? warning = null;
            if (info.Length > LargeFileBytes)
                warning = $"文件较大（{info.Length / 1024 / 1024} MB），已按纯文本打开，未启用语法高亮。";

            return new OpenResult
            {
                Path = path,
                Text = text,
                Encoding = encoding,
                LineEnding = EncodingDetector.DetectLineEnding(text),
                ByteCount = info.Length,
                LineCount = CountLines(text),
                HasBom = hasBom,
                Warning = warning,
            };
        }
        catch (UnsupportedBinaryFileException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not FileOpenException)
        {
            throw new FileOpenException(path, $"读取失败：{ex.Message}", ex);
        }
    }

    /// <summary>是否为不受支持的二进制内容（NUL 字节是主要判据）。</summary>
    public static bool LooksLikeBinary(byte[] bytes)
    {
        int scan = Math.Min(bytes.Length, BinaryScanBytes);
        for (int i = 0; i < scan; i++)
        {
            if (bytes[i] == 0) return true;
        }
        return false;
    }

    /// <summary>是否可以直接当文本打开（不抛异常的预检，供拖放 / 关联打开使用）。</summary>
    public static bool CanOpenAsText(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            using var stream = File.OpenRead(path);
            byte[] buffer = new byte[Math.Min(BinaryScanBytes, stream.Length)];
            int read = stream.Read(buffer, 0, buffer.Length);
            return !LooksLikeBinary(buffer[..read]);
        }
        catch
        {
            return false;
        }
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0) return 1;
        int count = 1;
        foreach (char c in text)
        {
            if (c == '\n') count++;
        }
        return count;
    }
}
