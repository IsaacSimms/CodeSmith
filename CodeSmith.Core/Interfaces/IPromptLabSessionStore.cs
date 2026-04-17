// == Prompt Lab Session Store Interface == //
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for storing and retrieving Prompt Lab sessions.
/// </summary>
public interface IPromptLabSessionStore
{
    PromptLabSession? Get(Guid sessionId);  // Retrieves a session by its identifier, or null if not found
    void Set(PromptLabSession session);      // Stores or updates a session
}
