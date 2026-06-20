// == Usage Options (free quota configuration) == //
namespace CodeSmith.Infrastructure.Configuration;

public class UsageOptions
{
    public const string SectionName = "Usage";

    public long FreeMonthlyTokenQuota { get; set; } = 20_000;

    /// <summary>
    /// Explicit list of objectIds that are allowed to use the X-Debug-User-Id header bypass.
    /// Empty in production. Only exact matches are honored.
    /// </summary>
    public string[] AllowedDebugObjectIds { get; set; } = Array.Empty<string>();
}
