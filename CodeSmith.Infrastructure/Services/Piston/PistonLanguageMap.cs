// == Piston Language Mapping == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Infrastructure.Services.Piston;

/// <summary>
/// Maps CodeSmith Language enum values to Piston runtime identifiers.
/// Piston uses string language names (e.g. "python", "c++") and supports
/// "*" as the version specifier to pick whatever version is installed.
/// </summary>
internal static class PistonLanguageMap
{
    private static readonly Dictionary<Language, PistonLanguage> Map = new()
    {
        [Language.Python]     = new("python",     "*", "main.py"),
        [Language.TypeScript] = new("typescript", "*", "main.ts"),
        [Language.Go]         = new("go",         "*", "main.go"),
        [Language.Cpp]        = new("c++",        "*", "main.cpp"),
        [Language.Rust]       = new("rust",       "*", "main.rs"),
        [Language.Java]       = new("java",       "*", "Main.java"),
        [Language.CSharp]     = new("mono",       "*", "main.cs"),
    };

    public static bool TryGet(Language language, out PistonLanguage value) => Map.TryGetValue(language, out value!);
}

internal readonly record struct PistonLanguage(string Language, string Version, string FileName);
