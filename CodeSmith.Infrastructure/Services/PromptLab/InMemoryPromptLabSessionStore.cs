// == In-Memory Prompt Lab Session Store == //
using System.Collections.Concurrent;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Infrastructure.Services.PromptLab;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IPromptLabSessionStore"/>
/// using a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
public class InMemoryPromptLabSessionStore : IPromptLabSessionStore
{
    private readonly ConcurrentDictionary<Guid, PromptLabSession> _sessions = new();

    public PromptLabSession? Get(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void Set(PromptLabSession session)
    {
        _sessions[session.SessionId] = session;
    }
}
