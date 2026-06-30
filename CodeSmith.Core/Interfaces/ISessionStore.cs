using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Interfaces;

public interface ISessionStore<TSession> where TSession : class
{
    TSession? Get(string sessionId);
    void Set(TSession session);

    // Runs action under a per-session lock so concurrent mutations of one session's shared mutable
    // state (chat history, attempts) are serialized — a Guidance Turn or an attempt submission for the
    // same session never interleaves. Different sessions run concurrently. The lock owns acquire/release
    // so callers cannot leak a held lock.
    Task<TResult> WithSessionLockAsync<TResult>(string sessionId, Func<Task<TResult>> action, CancellationToken ct = default);
}

// Backward-compat aliases for convenience
public interface ISessionStore : ISessionStore<ProblemSession> { }
public interface IPromptLabSessionStore : ISessionStore<PromptLabSession> { }
public interface ISystemLabSessionStore : ISessionStore<CodeSmith.Core.Models.SystemLab.SystemLabSession> { }
