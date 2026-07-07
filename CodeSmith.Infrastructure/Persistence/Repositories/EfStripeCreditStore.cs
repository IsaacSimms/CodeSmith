// == EF Stripe Credit Store == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.EntityFrameworkCore;

namespace CodeSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF adapter that applies a Stripe top-up atomically and idempotently. The dedup insert, the balance
/// credit, and the TopUp ledger row are staged on one DbContext and committed in a single SaveChangesAsync,
/// so the three either all land or none do. A concurrent balance write from usage enforcement surfaces as a
/// concurrency conflict and is retried; a concurrent redelivery of the same event surfaces as a duplicate
/// key and is treated as an already-processed replay.
/// </summary>
public class EfStripeCreditStore : IStripeCreditStore
{
    private readonly CodeSmithDbContext _db;

    private const int MaxConcurrencyRetries = 3;

    public EfStripeCreditStore(CodeSmithDbContext db)
    {
        _db = db;
    }

    public async Task<TopUpOutcome> ApplyTopUpAsync(string eventId, string objectId, decimal amountUsd, long freeQuotaMax, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            // Dedup: a prior delivery already credited this event — safe replay, no change.
            if (await _db.ProcessedStripeEvents.AnyAsync(e => e.EventId == eventId, ct))
                return TopUpOutcome.AlreadyProcessed;

            var balance = await _db.CreditBalances.FirstOrDefaultAsync(b => b.ObjectId == objectId, ct);
            if (balance is null)
            {
                balance = CreditBalance.CreateNew(objectId, freeQuotaMax);
                _db.CreditBalances.Add(balance);
            }

            balance.PaidCreditsBalance += amountUsd;

            _db.ProcessedStripeEvents.Add(new ProcessedStripeEvent { EventId = eventId });
            _db.UsageLedgerEntries.Add(new UsageLedgerEntry
            {
                ObjectId = objectId,
                Type = LedgerEntryType.TopUp,
                Provider = null,                // A top-up has no AI provider/model/tokens
                Model = null,
                InputTokens = 0,
                OutputTokens = 0,
                CostUsd = amountUsd,            // TopUp: credited amount (positive, added to balance)
                ProviderCostUsd = null,
                Feature = "Billing:TopUp",
                TimestampUtc = DateTime.UtcNow
            });

            try
            {
                // Single transaction: dedup marker + credit + ledger commit together or not at all.
                await _db.SaveChangesAsync(ct);
                return TopUpOutcome.Applied;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Usage enforcement bumped the balance RowVersion between our read and write. Discard the
                // failed attempt's tracked state and re-run the whole unit against fresh data.
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateException)
            {
                // Likely a concurrent redelivery inserted the dedup row first. If the event is now recorded,
                // the credit already happened elsewhere — treat as processed. Otherwise the failure is real.
                _db.ChangeTracker.Clear();
                if (await _db.ProcessedStripeEvents.AnyAsync(e => e.EventId == eventId, ct))
                    return TopUpOutcome.AlreadyProcessed;
                throw;
            }
        }
    }
}
