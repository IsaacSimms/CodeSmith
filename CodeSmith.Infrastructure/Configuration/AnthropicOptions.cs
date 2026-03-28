// == Anthropic Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the Anthropic API client.
/// Binds to the "Anthropic" section in appsettings.
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";                // Configuration section name
    public string ApiKey { get; set; } = string.Empty;             // The Anthropic API key. Must be provided via configuration, never hardcoded.
}
