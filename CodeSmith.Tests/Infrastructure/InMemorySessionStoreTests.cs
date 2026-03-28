// == In-Memory Session Store Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class InMemorySessionStoreTests
{
    private readonly InMemorySessionStore _store = new();

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        var result = _store.Get(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void Set_ThenGet_ReturnsSameSession()
    {
        var session = new ProblemSession
        {
            Difficulty = Difficulty.Medium,
            ProblemDescription = "Test problem"
        };

        _store.Set(session);
        var retrieved = _store.Get(session.SessionId);

        Assert.NotNull(retrieved);
        Assert.Equal(session.SessionId, retrieved.SessionId);
        Assert.Equal("Test problem", retrieved.ProblemDescription);
    }

    [Fact]
    public void Set_OverwritesExistingSession()
    {
        var session = new ProblemSession { ProblemDescription = "Original" };
        _store.Set(session);

        session.ProblemDescription = "Updated";
        _store.Set(session);

        var retrieved = _store.Get(session.SessionId);
        Assert.Equal("Updated", retrieved!.ProblemDescription);
    }

    [Fact]
    public void Set_MultipleSessions_RetrievesCorrectOne()
    {
        var session1 = new ProblemSession { ProblemDescription = "First" };
        var session2 = new ProblemSession { ProblemDescription = "Second" };

        _store.Set(session1);
        _store.Set(session2);

        Assert.Equal("First", _store.Get(session1.SessionId)!.ProblemDescription);
        Assert.Equal("Second", _store.Get(session2.SessionId)!.ProblemDescription);
    }

    [Fact]
    public void Get_AfterSet_PreservesAllProperties()
    {
        var session = new ProblemSession
        {
            Difficulty = Difficulty.Hard,
            ProblemDescription = "Hard problem",
            StarterCode = "public class Solution { }",
            Messages = [new ChatMessage { Role = CodeSmith.Core.Enums.MessageRole.User, Content = "Help" }]
        };

        _store.Set(session);
        var retrieved = _store.Get(session.SessionId);

        Assert.Equal(Difficulty.Hard, retrieved!.Difficulty);
        Assert.Equal("Hard problem", retrieved.ProblemDescription);
        Assert.Equal("public class Solution { }", retrieved.StarterCode);
        Assert.Single(retrieved.Messages);
    }
}
