// == Session Store Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for storing and retrieving problem sessions.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    /// Retrieves a session by its identifier.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The session if found; otherwise <c>null</c>.</returns>
    ProblemSession? Get(Guid sessionId);

    /// <summary>
    /// Stores or updates a session.
    /// </summary>
    /// <param name="session">The session to store.</param>
    void Set(ProblemSession session);
}
