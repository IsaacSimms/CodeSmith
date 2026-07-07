// == Processed Stripe Event Entity == //
namespace CodeSmith.Core.Models.Usage;

/// <summary>
/// Dedup record for Stripe webhook events. Stripe delivers at-least-once, so the webhook inserts one
/// row per event id before crediting; a primary-key collision means the event was already processed and
/// must be skipped. Guarantees a purchase credits the balance exactly once under redelivery.
/// </summary>
public class ProcessedStripeEvent
{
    public string EventId { get; set; } = string.Empty;            // Stripe event id (evt_...), primary key

    public DateTime ProcessedUtc { get; set; } = DateTime.UtcNow;  // When the webhook first processed this event
}
