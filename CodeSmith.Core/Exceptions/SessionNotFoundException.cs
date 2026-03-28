// == Session Not Found Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a requested session does not exist in the store.
/// </summary>
public class SessionNotFoundException : Exception
{
    /// <summary>The session ID that was not found.</summary>
    public Guid SessionId { get; }

    public SessionNotFoundException(Guid sessionId)
        : base($"Session '{sessionId}' not found.")
    {
        SessionId = sessionId;
    }

    public SessionNotFoundException(Guid sessionId, Exception innerException)
        : base($"Session '{sessionId}' not found.", innerException)
    {
        SessionId = sessionId;
    }
}
