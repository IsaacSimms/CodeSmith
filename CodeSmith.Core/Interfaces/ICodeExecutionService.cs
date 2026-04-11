// == Code Execution Service Interface == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Defines operations for executing user-submitted code in a sandboxed process.
/// </summary>
public interface ICodeExecutionService
{
    Task<CodeExecutionResult> ExecuteAsync(Language language, string code, CancellationToken ct = default); // Executes user code and returns stdout, stderr, exit code, and timeout status
}
