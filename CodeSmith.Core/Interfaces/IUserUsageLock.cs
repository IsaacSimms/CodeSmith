// == Per-User Usage Lock == //
using System.Threading;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Hands out a per-user async lock used to serialize usage check/record against the credit balance.
/// Prevents concurrent completions for the same user (e.g. Prompt Lab's parallel simulate/evaluate
/// fan-out) from racing on a shared DbContext or losing balance updates. Implementations return the
/// same SemaphoreSlim for a given objectId for the lifetime of the app, so this must be a singleton.
/// Mirrors the per-session lock idiom on <see cref="ISystemLabSessionStore"/>.
/// </summary>
public interface IUserUsageLock
{
    SemaphoreSlim GetLock(string objectId);
}
