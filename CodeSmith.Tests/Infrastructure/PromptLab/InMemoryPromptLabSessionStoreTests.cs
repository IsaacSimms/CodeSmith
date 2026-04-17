// == In-Memory Prompt Lab Session Store Tests == //
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services.PromptLab;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class InMemoryPromptLabSessionStoreTests
{
    private readonly InMemoryPromptLabSessionStore _store = new();

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        var result = _store.Get(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void Set_ThenGet_ReturnsSameSession()
    {
        var session = new PromptLabSession { ChallengeId = "format-json-01" };

        _store.Set(session);
        var retrieved = _store.Get(session.SessionId);

        Assert.NotNull(retrieved);
        Assert.Equal(session.SessionId, retrieved.SessionId);
        Assert.Equal("format-json-01", retrieved.ChallengeId);
    }

    [Fact]
    public void Set_OverwritesExistingSession()
    {
        var session = new PromptLabSession { ChallengeId = "original" };
        _store.Set(session);

        session.ChallengeId = "updated";
        _store.Set(session);

        var retrieved = _store.Get(session.SessionId);
        Assert.Equal("updated", retrieved!.ChallengeId);
    }

    [Fact]
    public void Set_MultipleSessions_RetrievesCorrectOne()
    {
        var session1 = new PromptLabSession { ChallengeId = "challenge-a" };
        var session2 = new PromptLabSession { ChallengeId = "challenge-b" };

        _store.Set(session1);
        _store.Set(session2);

        Assert.Equal("challenge-a", _store.Get(session1.SessionId)!.ChallengeId);
        Assert.Equal("challenge-b", _store.Get(session2.SessionId)!.ChallengeId);
    }

    [Fact]
    public void Get_AfterSet_PreservesAllProperties()
    {
        var attempt = new ChallengeAttempt
        {
            SystemPromptContent = "Be concise.",
            UserMessageContent = "List the planets.",
            TotalScore = 4,
            MaxScore = 5,
            OverallFeedback = "Good, but missed one criterion."
        };

        var session = new PromptLabSession
        {
            ChallengeId = "format-json-01",
            Attempts = [attempt]
        };

        _store.Set(session);
        var retrieved = _store.Get(session.SessionId);

        Assert.Equal("format-json-01", retrieved!.ChallengeId);
        Assert.Single(retrieved.Attempts);
        Assert.Equal(4, retrieved.Attempts[0].TotalScore);
    }
}
