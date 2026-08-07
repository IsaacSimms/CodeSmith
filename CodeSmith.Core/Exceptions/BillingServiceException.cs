// == Billing Service Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when the billing payment provider is unreachable or returns a transport/API failure that is not
/// a per-entry skip condition (e.g. missing Price). Mapped to 502 Bad Gateway so the purchase section can
/// fail independently of balance, ledger, and quota.
/// </summary>
public class BillingServiceException : Exception
{
    public BillingServiceException(string message)
        : base(message)
    {
    }

    public BillingServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
