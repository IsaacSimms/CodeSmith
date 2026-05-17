// == OpenAI Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the OpenAI API client.
/// Binds to the "OpenAi" section in appsettings.
/// </summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAi";                        // Configuration section name
    public string ApiKey        { get; set; } = string.Empty;          // The OpenAI API key. Must be provided via configuration, never hardcoded.
    public string AccurateModel { get; set; } = "gpt-4.1";            // Used for generation, evaluation, and test input creation
    public string FastModel     { get; set; } = "gpt-4.1-mini";       // Used for guidance and simulation — fast and cheap
    public int    ContextWindow { get; set; } = 1_047_576;             // Token limit for GPT-4.1 / GPT-4.1-mini
}
