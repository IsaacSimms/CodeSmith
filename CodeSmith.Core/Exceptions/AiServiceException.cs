// == AI Service Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when an AI provider API returns an error or the SDK call fails.
/// Wraps upstream exceptions with a clean, safe message.
/// </summary>
public class AiServiceException : Exception
{
    public AiServiceException(string message)
        : base(message)
    {
    }

    public AiServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
