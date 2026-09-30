// Copyright (c) 2026 ByteForge
using System.Text;

namespace ByteForge.FileIO;

/// <summary>打开结果。</summary>
public sealed class OpenResult
{
    public required string Path { get; init; }
    public required string Text { get; init; }
    public required Encoding Encoding { get; init; }
    public required string LineEnding { get; init; }
    public long ByteCount { get; init; }
    public int LineCount { get; init; }
    public bool HasBom { get; init; }

    /// <summary>非致命提示（例如文件过大已按纯文本打开）。</summary>
    public string? Warning { get; init; }

    /// <summary>文件后缀（含点，无后缀为空串）。</summary>
    public string Extension => System.IO.Path.GetExtension(Path);
}

/// <summary>文件含 NUL 字节等特征，判定为不受支持的二进制文件。</summary>
public sealed class UnsupportedBinaryFileException : Exception
{
    public UnsupportedBinaryFileException(string path)
        : base("该文件是不受支持的二进制文件") => Path = path;

    public string Path { get; }
}

/// <summary>文件不存在 / 无法读取。</summary>
public sealed class FileOpenException : Exception
{
    public FileOpenException(string path, string reason, Exception? inner = null)
        : base(reason, inner) => Path = path;

    public string Path { get; }
}
