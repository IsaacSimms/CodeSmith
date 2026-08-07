// == Stripe Billing Service == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CodeSmith.Infrastructure.Billing;

/// <summary>
/// Stripe adapter for the prepaid-credits billing seam. Creates hosted Checkout sessions, verifies and
/// processes completion webhooks (idempotently, via IStripeCreditStore), reads balance/ledger, and serves
/// the pack catalog for the caller. Never debits a balance and never depends on usage enforcement. The
/// metadata key "objectId" carries the payer identity from checkout creation through to the webhook.
/// </summary>
public class StripeBillingService : IBillingService
{
    private const string ObjectIdMetadataKey = "objectId";
    private const string CheckoutCompletedEvent = "checkout.session.completed";
    private const string UsdCurrency = "usd";
    private const string PacksCacheKey = "billing:packs";
    private static readonly TimeSpan PacksCacheTtl = TimeSpan.FromMinutes(5);

    private readonly StripeOptions _stripe;
    private readonly UsageOptions _usage;
    private readonly IStripeEventReader _eventReader;
    private readonly IStripeCreditStore _creditStore;
    private readonly ICreditBalanceRepository _balanceRepo;
    private readonly IUsageLedgerRepository _ledgerRepo;
    private readonly ICurrentUser _currentUser;
    private readonly IStripePriceReader _priceReader;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StripeBillingService> _logger;

    public StripeBillingService(
        IOptions<StripeOptions> stripe,
        IOptions<UsageOptions> usage,
        IStripeEventReader eventReader,
        IStripeCreditStore creditStore,
        ICreditBalanceRepository balanceRepo,
        IUsageLedgerRepository ledgerRepo,
        ICurrentUser currentUser,
        IStripePriceReader priceReader,
        IMemoryCache cache,
        ILogger<StripeBillingService> logger)
    {
        _stripe = stripe.Value;
        _usage = usage.Value;
        _eventReader = eventReader;
        _creditStore = creditStore;
        _balanceRepo = balanceRepo;
        _ledgerRepo = ledgerRepo;
        _currentUser = currentUser;
        _priceReader = priceReader;
        _cache = cache;
        _logger = logger;
    }

    // == CreateCheckoutSessionAsync == //

    public async Task<string> CreateCheckoutSessionAsync(string priceId, CancellationToken ct = default)
    {
        var objectId = RequireUser();

        // Allow-list: never create a session for an arbitrary price in the account.
        if (!_stripe.PriceIds.Contains(priceId))
            throw new InvalidPriceException(priceId);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = _stripe.SuccessUrl,
            CancelUrl = _stripe.CancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            Metadata = new Dictionary<string, string> { [ObjectIdMetadataKey] = objectId }
        };

        var sessions = new SessionService(new StripeClient(_stripe.SecretKey));
        var session = await sessions.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation("Created checkout session {SessionId} for {ObjectId} (price {PriceId})", session.Id, objectId, priceId);
        return session.Url;
    }

    // == HandleWebhookAsync == //

    public async Task<WebhookResult> HandleWebhookAsync(string requestBody, string signatureHeader, CancellationToken ct = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = _eventReader.Read(requestBody, signatureHeader, _stripe.WebhookSecret);
        }
        catch (StripeException)
        {
            // Never leak the raw Stripe error; surface a domain 400.
            throw new WebhookSignatureException("Webhook signature verification failed.");
        }

        if (stripeEvent.Type != CheckoutCompletedEvent || stripeEvent.Data.Object is not Session session)
        {
            _logger.LogInformation("Ignoring Stripe event {EventId} of type {Type}", stripeEvent.Id, stripeEvent.Type);
            return WebhookResult.Ignored;
        }

        if (!string.Equals(session.Currency, UsdCurrency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Ignoring checkout {EventId}: unsupported currency {Currency}", stripeEvent.Id, session.Currency);
            return WebhookResult.Ignored;
        }

        var objectId = session.Metadata is not null && session.Metadata.TryGetValue(ObjectIdMetadataKey, out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(objectId))
        {
            _logger.LogWarning("Ignoring checkout {EventId}: missing objectId metadata", stripeEvent.Id);
            return WebhookResult.Ignored;
        }

        if (session.AmountTotal is not > 0)
        {
            _logger.LogWarning("Ignoring checkout {EventId}: non-positive amount_total {Amount}", stripeEvent.Id, session.AmountTotal);
            return WebhookResult.Ignored;
        }

        var amountUsd = session.AmountTotal.Value / 100m; // integer minor units → USD (currency asserted above)

        var outcome = await _creditStore.ApplyTopUpAsync(stripeEvent.Id, objectId, amountUsd, _usage.FreeTokenQuota, ct);

        _logger.LogInformation("Processed checkout {EventId} for {ObjectId}: {Amount} USD → {Outcome}",
            stripeEvent.Id, objectId, amountUsd, outcome);

        return outcome == TopUpOutcome.Applied ? WebhookResult.Credited : WebhookResult.AlreadyProcessed;
    }

    // == Read endpoints == //

    public async Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        var objectId = RequireUser();
        var balance = await _balanceRepo.GetAsync(objectId, ct);
        return balance?.PaidCreditsBalance ?? 0m;
    }

    public async Task<IReadOnlyList<UsageLedgerEntry>> GetRecentLedgerAsync(int take, CancellationToken ct = default)
    {
        var objectId = RequireUser();
        return await _ledgerRepo.GetRecentAsync(objectId, take, ct);
    }

    // == Pack catalog == //

    public async Task<IReadOnlyList<CreditPack>> GetPacksAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(PacksCacheKey, out IReadOnlyList<CreditPack>? cached) && cached is not null)
            return cached;

        var packs = new List<CreditPack>(_stripe.PriceIds.Length);

        foreach (var priceId in _stripe.PriceIds)
        {
            Price price;
            try
            {
                price = await _priceReader.GetWithProductAsync(priceId, ct);
            }
            catch (StripeException ex) when (IsMissingResource(ex))
            {
                _logger.LogWarning(ex, "Skipping pack {PriceId}: price not found in Stripe", priceId);
                continue;
            }
            catch (StripeException ex)
            {
                // Transport / API failure — do not cache, surface 502 for the purchase section alone.
                throw new BillingServiceException("Unable to load credit packs from Stripe.", ex);
            }

            if (!TryMapPack(price, out var pack, out var skipReason))
            {
                _logger.LogWarning("Skipping pack {PriceId}: {Reason}", priceId, skipReason);
                continue;
            }

            packs.Add(pack);
        }

        IReadOnlyList<CreditPack> result = packs;
        _cache.Set(PacksCacheKey, result, PacksCacheTtl);
        return result;
    }

    // == Pack helpers == //

    private static bool TryMapPack(Price price, out CreditPack pack, out string skipReason)
    {
        pack = null!;
        skipReason = string.Empty;

        if (!price.Active)
        {
            skipReason = "price is inactive";
            return false;
        }

        if (!string.Equals(price.Currency, UsdCurrency, StringComparison.OrdinalIgnoreCase))
        {
            skipReason = $"unsupported currency '{price.Currency}'";
            return false;
        }

        var name = price.Product?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            skipReason = "blank product name";
            return false;
        }

        if (price.UnitAmount is not > 0)
        {
            skipReason = $"non-positive unit_amount {price.UnitAmount}";
            return false;
        }

        pack = new CreditPack
        {
            PriceId = price.Id,
            Name = name,
            Amount = price.UnitAmount.Value / 100m,
            Currency = price.Currency
        };
        return true;
    }

    private static bool IsMissingResource(StripeException ex)
        => ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound
           || string.Equals(ex.StripeError?.Code, "resource_missing", StringComparison.Ordinal);

    // objectId comes only from the authenticated principal — never from claims/headers read here directly.
    private string RequireUser()
    {
        var objectId = _currentUser.ObjectId;
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("An authenticated user is required for this billing operation.");
        return objectId;
    }
}
