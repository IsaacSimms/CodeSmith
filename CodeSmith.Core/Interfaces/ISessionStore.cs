using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Interfaces;

public interface ISessionStore<TSession> where TSession : class
{
    TSession? Get(string sessionId);
    void Set(TSession session);
}

// Backward-compat aliases for convenience
public interface ISessionStore : ISessionStore<ProblemSession> { }
public interface IPromptLabSessionStore : ISessionStore<PromptLabSession> { }
