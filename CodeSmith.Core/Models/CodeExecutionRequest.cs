// == Code Execution Request == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Input for sandboxed code execution. SessionId is optional for backends that
/// do not need correlation (Piston, LocalProcess) and required for backends that
/// reuse an external sandbox identity (Dynamic Sessions).
/// </summary>
public sealed class CodeExecutionRequest
{
    public required Language Language { get; init; } // Language to execute
    public required string Code { get; init; }       // Source text submitted by the user
    public Guid? SessionId { get; init; }            // Tutoring session id; Dynamic Sessions reuses this as pool identifier
}
