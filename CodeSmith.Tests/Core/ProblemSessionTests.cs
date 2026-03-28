// == Problem Session Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Tests.Core;

public class ProblemSessionTests
{
    [Fact]
    public void NewSession_HasUniqueSessionId()
    {
        var session1 = new ProblemSession();
        var session2 = new ProblemSession();

        Assert.NotEqual(Guid.Empty, session1.SessionId);
        Assert.NotEqual(session1.SessionId, session2.SessionId);
    }

    [Fact]
    public void NewSession_HasRecentCreatedAt()
    {
        var before = DateTime.UtcNow;
        var session = new ProblemSession();
        var after = DateTime.UtcNow;

        Assert.InRange(session.CreatedAt, before, after);
    }

    [Fact]
    public void NewSession_HasEmptyMessagesList()
    {
        var session = new ProblemSession();

        Assert.NotNull(session.Messages);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public void NewSession_DefaultsToEmptyStrings()
    {
        var session = new ProblemSession();

        Assert.Equal(string.Empty, session.ProblemDescription);
        Assert.Equal(string.Empty, session.StarterCode);
    }

    [Fact]
    public void Session_CanSetDifficulty()
    {
        var session = new ProblemSession { Difficulty = Difficulty.Hard };

        Assert.Equal(Difficulty.Hard, session.Difficulty);
    }
}
