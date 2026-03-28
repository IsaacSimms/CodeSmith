// == Anthropic Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the Anthropic API client.
/// Binds to the "Anthropic" section in appsettings.
/// </summary>
public class AnthropicOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Anthropic";

    /// <summary>The Anthropic API key. Must be provided via configuration, never hardcoded.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
