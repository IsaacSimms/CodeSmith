// == Usage Enforcer Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// Enforces free quota and paid credit checks before LLM calls (using upper bound).
/// Records actuals and deducts (free tokens first, then paid credits).
/// Strong consistency via EF transaction + RowVersion.
/// </summary>
public class UsageEnforcer : IUsageEnforcer
{
    private readonly ICreditBalanceRepository _balanceRepo;
    private readonly IUsageLedgerRepository _ledgerRepo;
    private readonly ILlmPricing _pricing;
    private readonly UsageOptions _options;
    private readonly ILogger<UsageEnforcer> _logger;

    public UsageEnforcer(
        ICreditBalanceRepository balanceRepo,
        IUsageLedgerRepository ledgerRepo,
        ILlmPricing pricing,
        IOptions<UsageOptions> options,
        ILogger<UsageEnforcer> logger)
    {
        _balanceRepo = balanceRepo;
        _ledgerRepo = ledgerRepo;
        _pricing = pricing;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CheckAndReserveAsync(string objectId, AiProvider provider, int estInputTokens, int estOutputTokens, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("objectId is required for usage enforcement.");

        var estCost = _pricing.EstimateUpperBoundCost(provider, estInputTokens, estOutputTokens);
        var estTotalTokens = (long)estInputTokens + estOutputTokens;

        var balance = await _balanceRepo.GetAsync(objectId, ct) ?? new CreditBalance { ObjectId = objectId, FreeQuotaMax = _options.FreeMonthlyTokenQuota };

        // Monthly reset (free quota)
        if (NeedsReset(balance.LastFreeResetUtc))
        {
            balance.FreeTokensUsedThisMonth = 0;
            balance.LastFreeResetUtc = DateTime.UtcNow;
        }

        var freeRemaining = balance.FreeQuotaMax - balance.FreeTokensUsedThisMonth;
        var hasFree = freeRemaining >= estTotalTokens;
        var hasPaid = balance.PaidCreditsBalance >= estCost;

        if (!hasFree && !hasPaid)
        {
            _logger.LogWarning("Quota/credit check failed for {ObjectId}. Free remaining: {Free}, Paid: {Paid}, estCost: {Cost}", objectId, freeRemaining, balance.PaidCreditsBalance, estCost);
            throw new InsufficientQuotaException(objectId, "Insufficient quota or credits for this request.");
        }
    }

    public async Task RecordActualAsync(string objectId, AiProvider provider, string model, int actualInput, int actualOutput, decimal costUsd, string? feature = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return;

        var actualTokens = (long)actualInput + actualOutput;

        // Load/create inside a simple transaction boundary via SaveChanges
        var balance = await _balanceRepo.GetAsync(objectId, ct) ?? new CreditBalance { ObjectId = objectId, FreeQuotaMax = _options.FreeMonthlyTokenQuota };

        if (NeedsReset(balance.LastFreeResetUtc))
        {
            balance.FreeTokensUsedThisMonth = 0;
            balance.LastFreeResetUtc = DateTime.UtcNow;
        }

        // Free first
        var freeRemaining = balance.FreeQuotaMax - balance.FreeTokensUsedThisMonth;
        if (freeRemaining >= actualTokens)
        {
            balance.FreeTokensUsedThisMonth += actualTokens;
        }
        else
        {
            // Fall back to (or continue with) paid credits
            balance.PaidCreditsBalance -= costUsd;
        }

        var entry = new UsageLedgerEntry
        {
            ObjectId = objectId,
            Provider = provider,
            Model = model,
            InputTokens = actualInput,
            OutputTokens = actualOutput,
            CostUsd = costUsd,
            Feature = feature,
            TimestampUtc = DateTime.UtcNow
        };

        await _ledgerRepo.AppendAsync(entry, ct);
        await _balanceRepo.SaveAsync(balance, ct);

        _logger.LogInformation("Recorded usage for {ObjectId}: {In}+{Out} tokens, cost {Cost} via {Provider}/{Model}", objectId, actualInput, actualOutput, costUsd, provider, model);
    }

    private static bool NeedsReset(DateTime lastResetUtc)
    {
        var now = DateTime.UtcNow;
        return lastResetUtc.Year != now.Year || lastResetUtc.Month != now.Month;
    }
}
