// == In-Memory Session Store == //
using System.Collections.Concurrent;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISessionStore"/>
/// using a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<Guid, ProblemSession> _sessions = new();

    public ProblemSession? Get(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void Set(ProblemSession session)
    {
        _sessions[session.SessionId] = session;
    }
}
