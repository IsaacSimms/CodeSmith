// == Code Execution Result Model == //
namespace CodeSmith.Core.Models;

/// <summary>
/// Represents the outcome of executing user code in a sandboxed process.
/// </summary>
public class CodeExecutionResult
{
    public string Stdout { get; set; } = string.Empty;   // Captured standard output
    public string Stderr { get; set; } = string.Empty;   // Captured standard error
    public int ExitCode { get; set; }                     // Process exit code (0 = success)
    public bool TimedOut { get; set; }                    // True if execution exceeded the timeout
}
