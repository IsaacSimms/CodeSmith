// == In-Memory Session Store Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class InMemorySessionStoreTests
{
    private readonly ISessionStore<ProblemSession> _store = new InMemorySessionStore<ProblemSession>();

    [Fact]
    public void Get_UnknownId_ReturnsNull()
    {
        var result = _store.Get(Guid.NewGuid().ToString());

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
        var retrieved = _store.Get(session.SessionId.ToString());

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

        var retrieved = _store.Get(session.SessionId.ToString());
        Assert.Equal("Updated", retrieved!.ProblemDescription);
    }

    [Fact]
    public void Set_MultipleSessions_RetrievesCorrectOne()
    {
        var session1 = new ProblemSession { ProblemDescription = "First" };
        var session2 = new ProblemSession { ProblemDescription = "Second" };

        _store.Set(session1);
        _store.Set(session2);

        Assert.Equal("First", _store.Get(session1.SessionId.ToString())!.ProblemDescription);
        Assert.Equal("Second", _store.Get(session2.SessionId.ToString())!.ProblemDescription);
    }

    [Fact]
    public void Get_AfterSet_PreservesAllProperties()
    {
        var session = new ProblemSession
        {
            Difficulty = Difficulty.Hard,
            Language = Language.Rust,
            ProblemDescription = "Hard problem",
            StarterCode = "fn solution() {}",
            Messages = [new ChatMessage { Role = CodeSmith.Core.Enums.MessageRole.User, Content = "Help" }]
        };

        _store.Set(session);
        var retrieved = _store.Get(session.SessionId.ToString());

        Assert.Equal(Difficulty.Hard, retrieved!.Difficulty);
        Assert.Equal(Language.Rust, retrieved.Language);
        Assert.Equal("Hard problem", retrieved.ProblemDescription);
        Assert.Equal("fn solution() {}", retrieved.StarterCode);
        Assert.Single(retrieved.Messages);
    }

    // == Per-session lock == //

    [Fact]
    public async Task WithSessionLockAsync_SameSession_SerializesAccess()
    {
        var store = new InMemorySessionStore<ProblemSession>();
        var inFlight = 0;
        var maxInFlight = 0;
        var sync = new object();

        async Task<int> Body()
        {
            var now = Interlocked.Increment(ref inFlight);
            lock (sync) maxInFlight = Math.Max(maxInFlight, now);
            await Task.Delay(20);
            Interlocked.Decrement(ref inFlight);
            return 0;
        }

        var tasks = Enumerable.Range(0, 10).Select(_ => store.WithSessionLockAsync("session-1", Body));
        await Task.WhenAll(tasks);

        Assert.Equal(1, maxInFlight); // never more than one critical section at a time for one session
    }

    [Fact]
    public async Task WithSessionLockAsync_DifferentSessions_RunConcurrently()
    {
        var store = new InMemorySessionStore<ProblemSession>();
        var started = 0;
        var bothStarted = new TaskCompletionSource();

        async Task<int> Body()
        {
            if (Interlocked.Increment(ref started) == 2) bothStarted.SetResult();
            await bothStarted.Task; // can only complete if both bodies run at once → distinct locks
            return 0;
        }

        var a = store.WithSessionLockAsync("session-a", Body);
        var b = store.WithSessionLockAsync("session-b", Body);

        await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, started);
    }
}
