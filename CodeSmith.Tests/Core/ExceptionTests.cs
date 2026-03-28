// == Custom Exception Tests == //
using CodeSmith.Core.Exceptions;

namespace CodeSmith.Tests.Core;

public class ExceptionTests
{
    [Fact]
    public void SessionNotFoundException_ContainsSessionId()
    {
        var sessionId = Guid.NewGuid();
        var ex = new SessionNotFoundException(sessionId);

        Assert.Equal(sessionId, ex.SessionId);
        Assert.Contains(sessionId.ToString(), ex.Message);
    }

    [Fact]
    public void SessionNotFoundException_WithInnerException_PreservesIt()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new SessionNotFoundException(Guid.NewGuid(), inner);

        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void AnthropicApiException_PreservesMessage()
    {
        var ex = new AnthropicApiException("API call failed");

        Assert.Equal("API call failed", ex.Message);
    }

    [Fact]
    public void AnthropicApiException_WithInnerException_PreservesIt()
    {
        var inner = new HttpRequestException("timeout");
        var ex = new AnthropicApiException("API call failed", inner);

        Assert.Same(inner, ex.InnerException);
    }
}
