// == Session Store Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for storing and retrieving problem sessions.
/// </summary>
public interface ISessionStore
{
    ProblemSession? Get(Guid sessionId);      // Retrieves a session by its identifier, or null if not found
    void Set(ProblemSession session);            // Stores or updates a session
}
