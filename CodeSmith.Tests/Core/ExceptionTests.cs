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
}
