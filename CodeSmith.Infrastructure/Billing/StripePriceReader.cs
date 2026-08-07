// == Stripe Price Reader == //
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Stripe;

namespace CodeSmith.Infrastructure.Billing;

/// <summary>
/// Stripe.net adapter for Price retrieve + product expand. Thin wrapper so pack-catalog logic stays
/// testable behind IStripePriceReader.
/// </summary>
public class StripePriceReader : IStripePriceReader
{
    private readonly StripeOptions _stripe;

    public StripePriceReader(IOptions<StripeOptions> stripe)
    {
        _stripe = stripe.Value;
    }

    // == GetWithProductAsync == //

    public Task<Price> GetWithProductAsync(string priceId, CancellationToken ct = default)
    {
        var service = new PriceService(new StripeClient(_stripe.SecretKey));
        var options = new PriceGetOptions();
        options.AddExpand("product");
        return service.GetAsync(priceId, options, cancellationToken: ct);
    }
}
