// == Per-User Usage Lock Registry == //
using System.Collections.Concurrent;
using CodeSmith.Core.Interfaces;

namespace CodeSmith.Infrastructure.Services.Usage;

/// <summary>
/// Singleton registry of per-user async locks. Returns a stable SemaphoreSlim per objectId so that
/// usage check/record for the same user are serialized across concurrent completions and across
/// requests. Must be registered as a singleton — a scoped instance would not serialize across scopes.
/// </summary>
public sealed class UserUsageLock : IUserUsageLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public SemaphoreSlim GetLock(string objectId)
        => _locks.GetOrAdd(objectId, _ => new SemaphoreSlim(1, 1));
}
