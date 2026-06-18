// == Usage Options (free quota configuration) == //
namespace CodeSmith.Infrastructure.Configuration;

public class UsageOptions
{
    public const string SectionName = "Usage";

    public long FreeMonthlyTokenQuota { get; set; } = 100_000;
}
