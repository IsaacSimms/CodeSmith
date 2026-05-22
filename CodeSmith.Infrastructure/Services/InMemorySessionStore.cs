using System.Collections.Concurrent;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Infrastructure.Services;

public class InMemorySessionStore<TSession> : ISessionStore<TSession> where TSession : class
{
    private readonly ConcurrentDictionary<string, TSession> _sessions = new();

    public TSession? Get(string sessionId)
        => _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public void Set(TSession session)
    {
        var sessionId = session switch
        {
            ProblemSession ps       => ps.SessionId.ToString(),
            PromptLabSession pls    => pls.SessionId.ToString(),
            SystemLabSession sls    => sls.SessionId.ToString(),
            _ => throw new ArgumentException($"Unknown session type: {session.GetType().Name}")
        };
        _sessions[sessionId] = session;
    }
}

// Concrete wrapper so ISystemLabSessionStore resolves from DI as a named singleton
internal sealed class InMemorySystemLabSessionStore : InMemorySessionStore<SystemLabSession>, ISystemLabSessionStore { }
