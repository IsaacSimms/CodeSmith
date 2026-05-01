// == OpenAI Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the OpenAI API client.
/// Binds to the "OpenAi" section in appsettings.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";          // Configuration section name
    public string ApiKey { get; set; } = string.Empty;   // The OpenAI API key. Must be provided via configuration, never hardcoded.
}
