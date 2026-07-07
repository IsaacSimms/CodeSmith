// == Webhook Signature Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a billing webhook payload fails signature verification (payload is not authentically from the
/// payment processor). Mapped to 400 Bad Request. Never carries the underlying processor exception outward.
/// </summary>
public class WebhookSignatureException : Exception
{
    public WebhookSignatureException(string message)
        : base(message)
    {
    }
}
