// == Unknown Provider Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a request supplies an AiProvider value that is not a defined enum member.
/// Mapped to 400 Bad Request.
/// </summary>
public class UnknownProviderException : Exception
{
    public UnknownProviderException(int providerValue)
        : base($"Unknown AI provider value '{providerValue}'. Use Anthropic, OpenAi, or Xai.")
    {
    }
}
