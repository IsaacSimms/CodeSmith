// == Anthropic Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the Anthropic API client.
/// Binds to the "Anthropic" section in appsettings.
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";                           // Configuration section name
    public string ApiKey       { get; set; } = string.Empty;                 // The Anthropic API key. Must be provided via configuration, never hardcoded.
    public string AccurateModel { get; set; } = "claude-sonnet-4-6";         // Used for generation, evaluation, and test input creation
    public string FastModel     { get; set; } = "claude-haiku-4-5-20251001"; // Used for guidance and simulation — fast and cheap
    public int    ContextWindow { get; set; } = 200_000;                     // Token limit shared by all Claude models used here
    public int    TimeoutSeconds { get; set; } = 120;                        // Per-call HTTP timeout — SDK default is 10 minutes, far too long for a live request
    public int    MaxRetries     { get; set; } = 0;                          // Transport auto-retry re-runs a metered completion (invisible provider cost + latency) — keep off
}
