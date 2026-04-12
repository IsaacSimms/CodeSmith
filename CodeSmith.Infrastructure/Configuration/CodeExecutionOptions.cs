// == Code Execution Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the code execution backend. Binds to the
/// "CodeExecution" section in appsettings. Selects between the Piston sandbox
/// (default, safe for production) and the in-process LocalProcess executor
/// (dev-only fallback).
/// </summary>
public class CodeExecutionOptions
{
    public const string SectionName = "CodeExecution";

    public string Backend { get; set; } = "Piston";             // "Piston" or "LocalProcess"
    public PistonOptions Piston { get; set; } = new();          // Piston-specific settings
}

/// <summary>
/// Configuration options for the Piston code execution sandbox.
/// </summary>
public class PistonOptions
{
    public string BaseUrl { get; set; } = "http://localhost:2000";   // Piston HTTP API base URL
    public int TimeoutSeconds { get; set; } = 15;                     // HTTP client timeout (must exceed run/compile timeouts)
    public int RunTimeoutMs { get; set; } = 10_000;                   // Per-run wall-clock timeout forwarded to Piston
    public int CompileTimeoutMs { get; set; } = 10_000;               // Per-compile wall-clock timeout forwarded to Piston
    public int MaxOutputLength { get; set; } = 10_000;                // Max chars of stdout/stderr returned to the client
}
