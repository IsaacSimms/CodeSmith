// == Xai Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the xAI API client.
/// Binds to the "Xai" section in appsettings.
/// </summary>
public class XaiOptions
{
    public const string SectionName = "Xai";                           // Configuration section name
    public string ApiKey        { get; set; } = string.Empty;          // The xAI API key (xai-...). Must be provided via configuration, never hardcoded.
    public string AccurateModel { get; set; } = "grok-4.3";            // Used for generation, evaluation, and test input creation
    public string FastModel     { get; set; } = "grok-4.3";            // Used for guidance and simulation (fast model or same flagship)
    public int    ContextWindow { get; set; } = 1_000_000;             // Token limit reported for responses (approximate for chosen models)
    public int    TimeoutSeconds { get; set; } = 120;                  // Per-call network timeout — a hung provider call must fail well before SDK defaults
}
