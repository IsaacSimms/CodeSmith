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
    public string AccurateModel { get; set; } = "claude-sonnet-5";           // Used for generation, evaluation, and test input creation
    public string FastModel     { get; set; } = "claude-haiku-4-5";          // Used for guidance and simulation — fast and cheap
    // The two tiers no longer share a window (Sonnet 5 is 1M, Haiku 4.5 is 200k). One field can only
    // express one, so it holds the smaller: the frontend TokenUsageBar under-reports headroom on the
    // Accurate tier rather than over-reporting it on Fast. Split per tier if the bar needs to be exact.
    public int    ContextWindow { get; set; } = 200_000;
    public int    TimeoutSeconds { get; set; } = 120;                        // Per-call HTTP timeout — SDK default is 10 minutes, far too long for a live request
    public int    MaxRetries     { get; set; } = 0;                          // Transport auto-retry re-runs a metered completion (invisible provider cost + latency) — keep off
    public int    StreamIdleTimeoutSeconds  { get; set; } = 30;              // Max silence between stream events (covers time-to-first-token) — a stalled provider fails fast
    public int    StreamTotalTimeoutSeconds { get; set; } = 300;             // Backstop for pathological slow-drip streams; healthy streams are bounded by MaxTokens well before this
}
