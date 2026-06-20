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
/// Enforces free quota and paid credit checks before LLM calls (using an upper bound), then records
/// actuals and deducts (free tokens first, then paid credits). Per-user serialization via
/// <see cref="IUserUsageLock"/> guarantees that concurrent completions for the same user (e.g. the
/// Prompt Lab parallel simulate/evaluate fan-out) cannot race on the shared scoped DbContext or lose
/// a balance update — the check and the read-modify-write each run under the user's lock.
/// </summary>
public class UsageEnforcer : IUsageEnforcer
{
    private readonly ICreditBalanceRepository _balanceRepo;
    private readonly IUsageLedgerRepository _ledgerRepo;
    private readonly IIpFreeUsageRepository _ipRepo;
    private readonly ILlmPricing _pricing;
    private readonly IUserUsageLock _locks;
    private readonly UsageOptions _options;
    private readonly ILogger<UsageEnforcer> _logger;

    public UsageEnforcer(
        ICreditBalanceRepository balanceRepo,
        IUsageLedgerRepository ledgerRepo,
        IIpFreeUsageRepository ipRepo,
        ILlmPricing pricing,
        IUserUsageLock locks,
        IOptions<UsageOptions> options,
        ILogger<UsageEnforcer> logger)
    {
        _balanceRepo = balanceRepo;
        _ledgerRepo = ledgerRepo;
        _ipRepo = ipRepo;
        _pricing = pricing;
        _locks = locks;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> CheckAndReserveAsync(string objectId, string? clientIp, AiProvider provider, int estInputTokens, int estOutputTokens, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("objectId is required for usage enforcement.");

        var estCost = _pricing.EstimateUpperBoundCost(provider, estInputTokens, estOutputTokens);
        var estTotalTokens = (long)estInputTokens + estOutputTokens;

        var normalizedIp = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp;
        var objectGate = _locks.GetLock(objectId);

        await objectGate.WaitAsync(ct);
        try
        {
            var balance = await _balanceRepo.GetAsync(objectId, ct)
                ?? new CreditBalance { ObjectId = objectId, FreeQuotaMax = _options.FreeMonthlyTokenQuota, FirstSeenUtc = DateTime.UtcNow };

            // 48h window per objectId (global first sighting). No monthly reset.
            var windowActive = (DateTime.UtcNow - balance.FirstSeenUtc).TotalHours < 48;

            var objectFreeRem = windowActive ? (balance.FreeQuotaMax - balance.FreeTokensUsedInWindow) : 0L;

            // IP aggregate cap (60k total free from this IP)
            var ipIssued = await _ipRepo.GetIssuedAsync(normalizedIp, ct);
            var ipRem = 60_000L - ipIssued;

            var hasFree = objectFreeRem >= estTotalTokens && ipRem >= estTotalTokens;
            var hasPaid = balance.PaidCreditsBalance >= estCost;

            if (!hasFree && !hasPaid)
            {
                // Lenient "last action" gate: allow the call that will exhaust the quota (if any remaining > 0)
                // so the user completes the intended action, then subsequent calls are blocked.
                bool hasObjectRoom = windowActive && objectFreeRem > 0;
                bool hasIpRoom = ipRem > 0;
                if (hasObjectRoom || (hasIpRoom && balance.FreeQuotaMax > 0))
                {
                    _logger.LogInformation("Permitting final free action for {ObjectId} (will exhaust remaining free quota or IP cap).", objectId);
                    return true; // will consume free
                }

                _logger.LogWarning("Quota/credit check failed for {ObjectId}. Free remaining: {Free}, IP rem: {IpRem}, Paid: {Paid}, estCost: {Cost}", objectId, objectFreeRem, ipRem, balance.PaidCreditsBalance, estCost);
                throw new InsufficientQuotaException(objectId, "Insufficient quota or credits for this request.");
            }

            return windowActive && objectFreeRem >= estTotalTokens; // indicates free will be used (for tier downgrade)
        }
        finally
        {
            objectGate.Release();
        }
    }

    public async Task RecordActualAsync(string objectId, string? clientIp, AiProvider provider, string model, int actualInput, int actualOutput, decimal costUsd, string? feature = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return;

        var actualTokens = (long)actualInput + actualOutput;

        var normalizedIp = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp;

        var objectGate = _locks.GetLock(objectId);
        await objectGate.WaitAsync(ct);

        SemaphoreSlim? ipGate = null;
        try
        {
            // Also serialize IP aggregate updates to prevent lost increments on the 60k cap
            ipGate = _locks.GetLock($"ip:{normalizedIp}");
            await ipGate.WaitAsync(ct);

            var balance = await _balanceRepo.GetAsync(objectId, ct)
                ?? new CreditBalance { ObjectId = objectId, FreeQuotaMax = _options.FreeMonthlyTokenQuota, FirstSeenUtc = DateTime.UtcNow };

            // Free first (only within active window)
            var windowActive = (DateTime.UtcNow - balance.FirstSeenUtc).TotalHours < 48;
            var freeRem = windowActive ? (balance.FreeQuotaMax - balance.FreeTokensUsedInWindow) : 0L;

            long freeUsedThisCall = 0;
            if (windowActive && freeRem >= actualTokens)
            {
                balance.FreeTokensUsedInWindow += actualTokens;
                freeUsedThisCall = actualTokens;
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

            // Increment IP aggregate only for the portion covered by free quota
            if (freeUsedThisCall > 0)
            {
                await _ipRepo.AddIssuedAsync(normalizedIp, freeUsedThisCall, ct);
            }

            _logger.LogInformation("Recorded usage for {ObjectId}: {In}+{Out} tokens, cost {Cost} via {Provider}/{Model} (free:{Free})", objectId, actualInput, actualOutput, costUsd, provider, model, freeUsedThisCall);
        }
        finally
        {
            ipGate?.Release();
            objectGate.Release();
        }
    }
}
