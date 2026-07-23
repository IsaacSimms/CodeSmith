// == Code Execution Service Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for executing user-submitted code in a sandboxed process.
/// </summary>
public interface ICodeExecutionService
{
    // Executes user code and returns stdout, stderr, exit code, and timeout status
    Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken ct = default);
}
