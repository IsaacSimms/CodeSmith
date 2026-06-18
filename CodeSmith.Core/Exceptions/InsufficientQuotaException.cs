// == Insufficient Quota Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown by the usage enforcement seam when the user has insufficient free quota or paid credits.
/// Mapped to 402 Payment Required.
/// </summary>
public class InsufficientQuotaException : Exception
{
    public string ObjectId { get; }

    public InsufficientQuotaException(string objectId, string message)
        : base(message)
    {
        ObjectId = objectId;
    }
}
