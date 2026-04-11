// == Code Execution Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when the code execution infrastructure fails (e.g. compiler not found,
/// temp directory errors). Not thrown for user code errors — those are captured
/// in CodeExecutionResult.Stderr.
/// </summary>
public class CodeExecutionException : Exception
{
    public CodeExecutionException(string message)
        : base(message)
    {
    }

    public CodeExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
