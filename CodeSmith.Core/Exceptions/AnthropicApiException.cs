// == Anthropic API Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when the Anthropic API returns an error or an SDK call fails.
/// </summary>
public class AnthropicApiException : Exception
{
    public AnthropicApiException(string message)
        : base(message)
    {
    }

    public AnthropicApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
