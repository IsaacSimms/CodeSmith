// == Stripe Credit Store Interface == //
using CodeSmith.Core.Models.Usage;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Atomic, idempotent persistence for Stripe credit top-ups. A single call dedups the event, credits the
/// payer's PaidCreditsBalance, and appends a TopUp ledger row — committed together so a purchase can never
/// mark-processed without crediting (or credit without recording). Deduplication uses the Stripe event id;
/// concurrent balance writes from usage enforcement are handled via optimistic-concurrency retry. This is
/// the only billing seam that mutates a balance.
/// </summary>
public interface IStripeCreditStore
{
    // Credits amountUsd to the payer's balance exactly once for the given Stripe event id.
    // freeQuotaMax seeds the one-time free grant if the payer has no balance row yet (paid before first LLM call).
    Task<TopUpOutcome> ApplyTopUpAsync(
        string eventId,
        string objectId,
        decimal amountUsd,
        long freeQuotaMax,
        CancellationToken ct = default);
}

public enum TopUpOutcome
{
    Applied = 0,          // Credit was newly applied and recorded
    AlreadyProcessed = 1  // Event id was seen before; no change made (safe replay)
}
