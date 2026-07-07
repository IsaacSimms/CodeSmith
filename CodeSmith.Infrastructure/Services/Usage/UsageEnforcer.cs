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
/// Enforces free quota and paid credit limits as a reserve → settle / release lifecycle.
/// <see cref="ReserveAsync"/> holds an upper-bound estimate against the user's free window quota, the
/// per-IP aggregate, and paid credits — and *persists* that hold before releasing the lock. Because the
/// hold is written, concurrent completions for the same user (the Prompt Lab parallel simulate/evaluate
/// fan-out) can no longer all pass the same gate: each reservation sees the prior holds. Per-user
/// serialization via <see cref="IUserUsageLock"/> guards the read-modify-write of the shared scoped
/// DbContext, and an additional <c>ip:</c> lock guards the per-IP aggregate. <see cref="SettleAsync"/>
/// reverses the hold and applies the real cost (plus the ledger entry); <see cref="ReleaseAsync"/>
/// refunds the hold when the call produced nothing billable.
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

    private const long IpFreeTokenCap = 60_000; // Aggregate free-token cap per client IP across all objectIds

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

    // == ReserveAsync == //

    public async Task<UsageReservation> ReserveAsync(string objectId, string? clientIp, AiProvider provider, int estInputTokens, int estOutputTokens, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("objectId is required for usage enforcement.");

        var estCost = _pricing.EstimateUpperBoundCost(provider, estInputTokens, estOutputTokens);
        var estTotalTokens = (long)estInputTokens + estOutputTokens;
        var normalizedIp = Normalize(clientIp);

        var objectGate = _locks.GetLock(objectId);
        await objectGate.WaitAsync(ct);

        SemaphoreSlim? ipGate = null;
        try
        {
            ipGate = _locks.GetLock($"ip:{normalizedIp}");
            await ipGate.WaitAsync(ct);

            var balance = await GetOrCreateBalanceAsync(objectId, ct);

            var windowActive = WindowActive(balance);
            var objectFreeRem = windowActive ? (balance.FreeQuotaMax - balance.FreeTokensUsedInWindow) : 0L;

            var ipIssued = await _ipRepo.GetIssuedAsync(normalizedIp, ct);
            var ipRem = IpFreeTokenCap - ipIssued;

            var hasFreeStrict = windowActive && objectFreeRem >= estTotalTokens && ipRem >= estTotalTokens;
            var hasPaid = balance.PaidCreditsBalance >= estCost;

            // Decide what to hold: full-free, else full-paid, else partial free + paid overflow.
            long reservedFree;
            decimal reservedPaid;

            if (hasFreeStrict)
            {
                reservedFree = estTotalTokens;
                reservedPaid = 0m;
            }
            else if (hasPaid)
            {
                reservedFree = 0;
                reservedPaid = estCost;
            }
            else
            {
                var freeCover = ComputeFreeCover(windowActive, objectFreeRem, ipRem, estTotalTokens);
                var overflowCost = 0m;
                var permitted = false;

                if (freeCover > 0)
                {
                    var overflowTokens = estTotalTokens - freeCover;
                    var (overflowInput, overflowOutput) = SplitTokensProportionally(estInputTokens, estOutputTokens, overflowTokens, estTotalTokens);
                    overflowCost = _pricing.EstimateUpperBoundCost(provider, overflowInput, overflowOutput);
                    permitted = overflowCost == 0m || balance.PaidCreditsBalance >= overflowCost;
                }

                if (!permitted)
                {
                    _logger.LogWarning(
                        "Quota/credit reservation failed for {ObjectId}. WindowActive: {Window}, Free remaining: {Free}, IP rem: {IpRem}, Paid: {Paid}, estCost: {Cost}",
                        objectId, windowActive, objectFreeRem, ipRem, balance.PaidCreditsBalance, estCost);
                    throw new InsufficientQuotaException(objectId, "Insufficient quota or credits for this request.");
                }

                reservedFree = freeCover;
                reservedPaid = overflowCost;
            }

            // Persist the hold so concurrent reservations for this user see the reduced balance.
            if (reservedFree > 0)
                balance.FreeTokensUsedInWindow += reservedFree;
            if (reservedPaid > 0)
                balance.PaidCreditsBalance -= reservedPaid;

            await _balanceRepo.SaveAsync(balance, ct);

            if (reservedFree > 0)
                await _ipRepo.AddIssuedAsync(normalizedIp, reservedFree, ct);

            _logger.LogInformation(
                "Reserved for {ObjectId}: free {Free} tokens, paid {Paid} (est {Est} tokens) via {Provider}",
                objectId, reservedFree, reservedPaid, estTotalTokens, provider);

            return new UsageReservation
            {
                ObjectId = objectId,
                ClientIp = normalizedIp,
                Provider = provider,
                ReservedFreeTokens = reservedFree,
                ReservedPaidUsd = reservedPaid
            };
        }
        finally
        {
            ipGate?.Release();
            objectGate.Release();
        }
    }

    // == SettleAsync == //

    public async Task SettleAsync(UsageReservation reservation, string model, int actualInput, int actualOutput, decimal chargeUsd, decimal providerCostUsd, string? feature = null, CancellationToken ct = default)
    {
        if (reservation is null || string.IsNullOrWhiteSpace(reservation.ObjectId)) return;

        var objectId = reservation.ObjectId;
        var normalizedIp = reservation.ClientIp;
        var actualTokens = (long)actualInput + actualOutput;

        var objectGate = _locks.GetLock(objectId);
        await objectGate.WaitAsync(ct);

        SemaphoreSlim? ipGate = null;
        try
        {
            ipGate = _locks.GetLock($"ip:{normalizedIp}");
            await ipGate.WaitAsync(ct);

            var balance = await GetOrCreateBalanceAsync(objectId, ct);

            // 1) Reverse the hold taken at reserve time.
            ReverseHold(balance, reservation);

            // 2) Apply the actual deduction: free first (within the active window, bounded by both the
            //    objectId and IP remainders), then paid credits for the remainder.
            var windowActive = WindowActive(balance);
            var freeRem = windowActive ? (balance.FreeQuotaMax - balance.FreeTokensUsedInWindow) : 0L;

            var ipIssued = await _ipRepo.GetIssuedAsync(normalizedIp, ct);
            var ipIssuedAfterReverse = Math.Max(0, ipIssued - reservation.ReservedFreeTokens); // the hold is being reconciled
            var ipRem = IpFreeTokenCap - ipIssuedAfterReverse;

            var freeUsedThisCall = ComputeFreeCover(windowActive, freeRem, ipRem, actualTokens);
            if (freeUsedThisCall > 0)
                balance.FreeTokensUsedInWindow += freeUsedThisCall;

            var paidTokens = actualTokens - freeUsedThisCall;
            if (paidTokens > 0 && actualTokens > 0)
                balance.PaidCreditsBalance -= chargeUsd * paidTokens / actualTokens;

            var entry = new UsageLedgerEntry
            {
                ObjectId = objectId,
                Provider = reservation.Provider,
                Model = model,
                InputTokens = actualInput,
                OutputTokens = actualOutput,
                CostUsd = chargeUsd,              // amount charged to the customer
                ProviderCostUsd = providerCostUsd, // raw provider cost (margin = CostUsd - ProviderCostUsd)
                Feature = feature,
                TimestampUtc = DateTime.UtcNow
            };

            await _ledgerRepo.AppendAsync(entry, ct);
            await _balanceRepo.SaveAsync(balance, ct);

            // Net IP change: remove the reserved hold, add back the actual free portion.
            var netIp = freeUsedThisCall - reservation.ReservedFreeTokens;
            if (netIp != 0)
                await _ipRepo.AddIssuedAsync(normalizedIp, netIp, ct);

            _logger.LogInformation(
                "Settled usage for {ObjectId}: {In}+{Out} tokens, charge {Charge} (cost {Cost}) via {Provider}/{Model} (free:{Free})",
                objectId, actualInput, actualOutput, chargeUsd, providerCostUsd, reservation.Provider, model, freeUsedThisCall);
        }
        finally
        {
            ipGate?.Release();
            objectGate.Release();
        }
    }

    // == ReleaseAsync == //

    public async Task ReleaseAsync(UsageReservation reservation, CancellationToken ct = default)
    {
        if (reservation is null || string.IsNullOrWhiteSpace(reservation.ObjectId)) return;
        if (reservation.ReservedFreeTokens == 0 && reservation.ReservedPaidUsd == 0) return;

        var objectId = reservation.ObjectId;
        var normalizedIp = reservation.ClientIp;

        var objectGate = _locks.GetLock(objectId);
        await objectGate.WaitAsync(ct);

        SemaphoreSlim? ipGate = null;
        try
        {
            ipGate = _locks.GetLock($"ip:{normalizedIp}");
            await ipGate.WaitAsync(ct);

            var balance = await _balanceRepo.GetAsync(objectId, ct);
            if (balance is not null)
            {
                ReverseHold(balance, reservation);
                await _balanceRepo.SaveAsync(balance, ct);
            }

            if (reservation.ReservedFreeTokens > 0)
                await _ipRepo.AddIssuedAsync(normalizedIp, -reservation.ReservedFreeTokens, ct);

            _logger.LogInformation("Released reservation for {ObjectId}: free {Free}, paid {Paid}", objectId, reservation.ReservedFreeTokens, reservation.ReservedPaidUsd);
        }
        finally
        {
            ipGate?.Release();
            objectGate.Release();
        }
    }

    // == Balance helpers == //

    private async Task<CreditBalance> GetOrCreateBalanceAsync(string objectId, CancellationToken ct)
        => await _balanceRepo.GetOrCreateAsync(objectId, _options.FreeMonthlyTokenQuota, ct);

    // 48h window per objectId (global first sighting). No monthly reset.
    private static bool WindowActive(CreditBalance balance)
        => (DateTime.UtcNow - balance.FirstSeenUtc).TotalHours < 48;

    // Undoes a reservation's hold on the balance (free tokens floored at zero; paid credits refunded).
    private static void ReverseHold(CreditBalance balance, UsageReservation reservation)
    {
        if (reservation.ReservedFreeTokens > 0)
            balance.FreeTokensUsedInWindow = Math.Max(0, balance.FreeTokensUsedInWindow - reservation.ReservedFreeTokens);
        if (reservation.ReservedPaidUsd > 0)
            balance.PaidCreditsBalance += reservation.ReservedPaidUsd;
    }

    private static string Normalize(string? clientIp)
        => string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp;

    // == Quota helpers == //

    private static long ComputeFreeCover(bool windowActive, long objectFreeRem, long ipRem, long totalTokens)
    {
        if (!windowActive || objectFreeRem <= 0 || ipRem <= 0 || totalTokens <= 0)
            return 0;
        return Math.Min(objectFreeRem, Math.Min(ipRem, totalTokens));
    }

    private static (int Input, int Output) SplitTokensProportionally(int input, int output, long portion, long total)
    {
        if (total <= 0 || portion <= 0)
            return (0, 0);
        if (portion >= total)
            return (input, output);

        var inputPortion = (int)Math.Round(input * (double)portion / total);
        var outputPortion = (int)(portion - inputPortion);
        return (inputPortion, outputPortion);
    }
}
