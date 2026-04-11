// == Run Code Response DTO == //
namespace CodeSmith.Api.DTOs;

/// <summary>
/// Response body containing the results of code execution.
/// </summary>
public class RunCodeResponse
{
    public string Stdout { get; set; } = string.Empty;   // Captured standard output
    public string Stderr { get; set; } = string.Empty;   // Captured standard error
    public int ExitCode { get; set; }                     // Process exit code (0 = success)
    public bool TimedOut { get; set; }                    // True if execution exceeded the timeout
}
