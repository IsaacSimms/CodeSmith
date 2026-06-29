// == AI Provider Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Top-level AI configuration. Controls which provider is active at startup.
/// Binds to the "Ai" section in appsettings.
/// </summary>
public class AiOptions
{
    public const string SectionName = "Ai";                          // Configuration section name
    public string ActiveProvider { get; set; } = "Xai";              // The default provider. Must match an AiProvider enum value name.
}
