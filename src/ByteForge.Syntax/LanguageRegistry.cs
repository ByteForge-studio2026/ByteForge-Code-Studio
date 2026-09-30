// Copyright (c) 2026 ByteForge
using ByteForge.Core;

namespace ByteForge.Syntax;

/// <summary>语言注册表：按后缀取语言定义。</summary>
public static class LanguageRegistry
{
    private static readonly LanguageDefinition[] Definitions =
    {
        Python(), C(), Cpp(), Java(), CSharp(), Kotlin(), Php(), Xaml(), Xml(), Markdown(),
    };

    private static readonly Dictionary<string, LanguageDefinition> ByExtension =
        BuildExtensionMap();

    private static readonly Dictionary<string, LanguageDefinition> ById =
        Definitions.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    /// <summary>纯文本（无高亮）。</summary>
    public static LanguageDefinition PlainText { get; } = new()
    {
        Id = "plaintext",
        DisplayName = "纯文本",
        Extensions = Array.Empty<string>(),
    };

    public static IEnumerable<LanguageDefinition> All => Definitions;

    /// <summary>按文件名或后缀取语言；未知 / 无后缀返回纯文本。</summary>
    public static LanguageDefinition GetByExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension)) return PlainText;

        string ext = Path.GetExtension(pathOrExtension);
        if (string.IsNullOrEmpty(ext)) ext = pathOrExtension.StartsWith('.') ? pathOrExtension : "";
        if (string.IsNullOrEmpty(ext)) return PlainText;

        ext = ext.TrimStart('.').ToLowerInvariant();
        return ByExtension.TryGetValue(ext, out var def) ? def : PlainText;
    }

    public static LanguageDefinition GetById(string id) =>
        ById.TryGetValue(id, out var def) ? def : PlainText;

    private static Dictionary<string, LanguageDefinition> BuildExtensionMap()
    {
        var map = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in Definitions)
        {
            foreach (string ext in def.Extensions)
            {
                map[ext] = def;
            }
        }
        return map;
    }

    // ------------------------------------------------------------ 各语言定义

    private static LanguageDefinition Python() => new()
    {
        Id = "python",
        DisplayName = "Python",
        Extensions = new[] { "py" },
        LineComment = "#",
        TripleQuote = "\"\"\"",
        QuoteChars = new[] { '"', '\'' },
        Keywords = new HashSet<string>
        {
            "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
            "elif", "else", "except", "finally", "for", "from", "global", "if", "import", "in",
            "is", "lambda", "nonlocal", "not", "or", "pass", "raise", "return", "try", "while",
            "with", "yield",
        },
        Types = new HashSet<string> { "self", "cls", "True", "False", "None", "int", "str", "float", "list", "dict", "set", "tuple", "bool" },
    };

    private static LanguageDefinition C() => new()
    {
        Id = "c",
        DisplayName = "C",
        Extensions = new[] { "c", "h" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsCharLiteral = true,
        SupportsPreprocessor = true,
        QuoteChars = new[] { '"' },
        Keywords = new HashSet<string>
        {
            "auto", "break", "case", "const", "continue", "default", "do", "else", "enum", "extern",
            "for", "goto", "if", "inline", "register", "restrict", "return", "sizeof", "static",
            "struct", "switch", "typedef", "union", "volatile", "while", "_Bool",
        },
        Types = new HashSet<string> { "void", "char", "short", "int", "long", "float", "double", "signed", "unsigned", "size_t" },
    };

    private static LanguageDefinition Cpp() => new()
    {
        Id = "cpp",
        DisplayName = "C++",
        Extensions = new[] { "cpp", "hpp", "cc", "cxx", "hh" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsCharLiteral = true,
        SupportsPreprocessor = true,
        QuoteChars = new[] { '"' },
        Keywords = new HashSet<string>
        {
            "alignas", "auto", "break", "case", "catch", "class", "const", "constexpr", "const_cast",
            "continue", "decltype", "default", "delete", "do", "dynamic_cast", "else", "enum",
            "explicit", "export", "extern", "for", "friend", "goto", "if", "inline", "mutable",
            "namespace", "new", "noexcept", "operator", "private", "protected", "public",
            "reinterpret_cast", "return", "sizeof", "static", "static_cast", "struct", "switch",
            "template", "this", "throw", "try", "typedef", "typeid", "typename", "union", "using",
            "virtual", "volatile", "while",
        },
        Types = new HashSet<string> { "bool", "char", "double", "float", "int", "long", "short", "signed", "unsigned", "void", "wchar_t", "size_t", "auto" },
    };

    private static LanguageDefinition Java() => new()
    {
        Id = "java",
        DisplayName = "Java",
        Extensions = new[] { "java" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsCharLiteral = true,
        QuoteChars = new[] { '"' },
        Keywords = new HashSet<string>
        {
            "abstract", "assert", "break", "case", "catch", "class", "const", "continue", "default",
            "do", "else", "enum", "extends", "final", "finally", "for", "goto", "if", "implements",
            "import", "instanceof", "interface", "native", "new", "package", "private", "protected",
            "public", "record", "return", "sealed", "static", "strictfp", "super", "switch",
            "synchronized", "this", "throw", "throws", "transient", "try", "var", "volatile", "while",
        },
        Types = new HashSet<string> { "boolean", "byte", "char", "double", "float", "int", "long", "short", "void", "String", "Object", "var" },
    };

    private static LanguageDefinition CSharp() => new()
    {
        Id = "csharp",
        DisplayName = "C#",
        Extensions = new[] { "cs" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsCharLiteral = true,
        SupportsPreprocessor = true,
        QuoteChars = new[] { '"' },
        Keywords = new HashSet<string>
        {
            "abstract", "as", "async", "await", "base", "break", "case", "catch", "checked", "class",
            "const", "continue", "default", "delegate", "do", "else", "enum", "event", "explicit",
            "extern", "finally", "fixed", "for", "foreach", "goto", "if", "implicit", "in", "interface",
            "internal", "is", "lock", "namespace", "new", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "record", "ref", "return", "sealed",
            "sizeof", "stackalloc", "static", "struct", "switch", "this", "throw", "try", "typeof",
            "unchecked", "unsafe", "using", "virtual", "void", "volatile", "while",
        },
        Types = new HashSet<string> { "bool", "byte", "char", "decimal", "double", "dynamic", "float", "int", "long", "nint", "nuint", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "var" },
    };

    private static LanguageDefinition Kotlin() => new()
    {
        Id = "kotlin",
        DisplayName = "Kotlin",
        Extensions = new[] { "kt", "kts" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsCharLiteral = true,
        QuoteChars = new[] { '"' },
        Keywords = new HashSet<string>
        {
            "as", "break", "class", "companion", "continue", "data", "do", "else", "enum", "for",
            "fun", "if", "import", "in", "init", "inline", "interface", "internal", "is", "object",
            "package", "private", "protected", "public", "return", "sealed", "super", "suspend",
            "this", "throw", "try", "typealias", "val", "var", "when", "while",
        },
        Types = new HashSet<string> { "Boolean", "Byte", "Char", "Double", "Float", "Int", "Long", "Short", "String", "Unit", "Any", "List", "MutableList", "Map" },
    };

    private static LanguageDefinition Php() => new()
    {
        Id = "php",
        DisplayName = "PHP",
        Extensions = new[] { "php" },
        LineComment = "//",
        BlockCommentStart = "/*",
        BlockCommentEnd = "*/",
        SupportsPreprocessor = true,
        QuoteChars = new[] { '"', '\'' },
        Keywords = new HashSet<string>
        {
            "abstract", "and", "array", "as", "break", "callable", "case", "catch", "class", "clone",
            "const", "continue", "declare", "default", "do", "echo", "else", "elseif", "empty",
            "endswitch", "endwhile", "extends", "final", "finally", "for", "foreach", "function",
            "global", "if", "implements", "include", "include_once", "instanceof", "insteadof",
            "interface", "isset", "list", "namespace", "new", "or", "print", "private", "protected",
            "public", "require", "require_once", "return", "static", "switch", "throw", "trait",
            "try", "unset", "use", "var", "while", "yield",
        },
        Types = new HashSet<string> { "true", "false", "null", "int", "float", "string", "bool", "array", "object", "mixed", "void" },
    };

    private static LanguageDefinition Xaml() => new()
    {
        Id = "xaml",
        DisplayName = "XAML",
        Extensions = new[] { "xaml" },
        IsMarkup = true,
    };

    private static LanguageDefinition Xml() => new()
    {
        Id = "xml",
        DisplayName = "XML",
        Extensions = new[] { "xml" },
        IsMarkup = true,
    };

    private static LanguageDefinition Markdown() => new()
    {
        Id = "markdown",
        DisplayName = "Markdown",
        Extensions = new[] { "md" },
        IsMarkdown = true,
    };
}
