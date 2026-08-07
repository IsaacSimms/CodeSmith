// == AI Provider Configuration Options == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Top-level AI configuration. Controls which provider is active at startup.
/// Binds to the "Ai" section in appsettings.
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";                          // Configuration section name
    // Applied when a client omits provider on any of the four LLM-creating endpoints.
    // Bound as AiProvider and validated at host start (ValidateOnStart) — a typo fails boot.
    public AiProvider ActiveProvider { get; set; } = AiProvider.Xai;
}
