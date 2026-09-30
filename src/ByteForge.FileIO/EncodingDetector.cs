// Copyright (c) 2026 ByteForge
using System.Text;

namespace ByteForge.FileIO;

/// <summary>
/// 编码识别：BOM 优先，其次严格 UTF-8 校验，再退回本地 ANSI（GB18030 等）。
/// 不引入第三方依赖，用系统自带的编码提供程序。
/// </summary>
public static class EncodingDetector
{
    /// <summary>探测编码；返回 (编码, 是否带 BOM)。</summary>
    public static (Encoding Encoding, bool HasBom) Detect(byte[] bytes, int length)
    {
        // UTF-8 BOM
        if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (new UTF8Encoding(true), true);

        // UTF-32 LE / BE（4 字节 BOM 要先判，否则会误判成 UTF-16）
        if (length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
            return (new UTF32Encoding(false, true), true);
        if (length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            return (new UTF32Encoding(true, true), true);

        // UTF-16 LE / BE
        if (length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (new UnicodeEncoding(false, true), true);
        if (length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (new UnicodeEncoding(true, true), true);

        // 无 BOM：先按严格 UTF-8 校验
        if (IsValidUtf8(bytes, length))
            return (new UTF8Encoding(false), false);

        // 退回本地 ANSI（简体中文环境即 GB18030）
        Encoding? local = TryGetLocalEncoding();
        return (local ?? new UTF8Encoding(false), false);
    }

    private static Encoding? TryGetLocalEncoding()
    {
        // .NET 默认不带 GB18030；若宿主注册了 CodePagesEncodingProvider 则可用。
        try
        {
            return Encoding.GetEncoding("GB18030");
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidUtf8(byte[] bytes, int length)
    {
        int i = 0;
        while (i < length)
        {
            byte b = bytes[i];
            int extra;
            int min;

            if (b <= 0x7F) { i++; continue; }
            else if ((b & 0xE0) == 0xC0) { extra = 1; min = 0x80; }
            else if ((b & 0xF0) == 0xE0) { extra = 2; min = 0x800; }
            else if ((b & 0xF8) == 0xF0) { extra = 3; min = 0x10000; }
            else return false;

            if (i + extra >= length) return false;

            int codePoint = b & (0x7F >> extra);
            for (int j = 1; j <= extra; j++)
            {
                byte next = bytes[i + j];
                if ((next & 0xC0) != 0x80) return false;
                codePoint = (codePoint << 6) | (next & 0x3F);
            }

            if (codePoint < min || codePoint > 0x10FFFF) return false;
            if (codePoint >= 0xD800 && codePoint <= 0xDFFF) return false;
            i += extra + 1;
        }
        return true;
    }

    /// <summary>识别换行符：CRLF / LF / CR，默认取系统换行。</summary>
    public static string DetectLineEnding(string text)
    {
        int crlf = text.Contains("\r\n") ? 1 : 0;
        if (crlf == 1) return "\r\n";
        if (text.Contains('\n')) return "\n";
        if (text.Contains('\r')) return "\r";
        return Environment.NewLine;
    }
}
