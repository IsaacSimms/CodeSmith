// == Invalid Price Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a checkout request supplies a priceId that is not in the configured allow-list.
/// Mapped to 400 Bad Request.
/// </summary>
public class InvalidPriceException : Exception
{
    public InvalidPriceException(string priceId)
        : base($"Price '{priceId}' is not a purchasable credit pack.")
    {
    }
}
