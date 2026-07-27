// == Executor Language Mapping == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Infrastructure.Services.Executor;

/// <summary>
/// Maps CodeSmith Language enum values to the language keys CodeSmith.Executor accepts
/// on POST /execute. This is a stable wire contract with that image — changing a key here
/// requires the matching change in CodeSmith.Executor's LanguageRunner switch.
/// </summary>
internal static class ExecutorLanguageMap
{
    private static readonly Dictionary<Language, string> Map = new()
    {
        [Language.Python]     = "python",
        [Language.TypeScript] = "typescript",
        [Language.Go]         = "go",
        [Language.Cpp]        = "cpp",
        [Language.Rust]       = "rust",
        [Language.Java]       = "java",
        [Language.CSharp]     = "csharp",
    };

    public static bool TryGet(Language language, out string value) => Map.TryGetValue(language, out value!);
}
