// == Code Execution Configuration Options == //
namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the code execution backend. Binds to the
/// "CodeExecution" section in appsettings. Selects between Piston (local Docker),
/// LocalProcess (dev-only), and DynamicSessions (Azure Hyper-V sandboxes).
/// </summary>
public class CodeExecutionOptions
{
    public const string SectionName = "CodeExecution";

    public string Backend { get; set; } = "Piston";                               // "Piston", "LocalProcess", or "DynamicSessions"
    public PistonOptions Piston { get; set; } = new();                            // Piston-specific settings
    public DynamicSessionsOptions DynamicSessions { get; set; } = new();          // Azure Dynamic Sessions settings
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

/// <summary>
/// Configuration for Azure Container Apps custom Dynamic Sessions code execution.
/// </summary>
public class DynamicSessionsOptions
{
    public string PoolManagementEndpoint { get; set; } = string.Empty; // Session pool root URL (no trailing path)
    public string ExecutePath { get; set; } = "/execute";              // Path forwarded into the custom container
    public int TimeoutSeconds { get; set; } = 120;                     // HTTP client timeout (covers cold start + run)
    public int RunTimeoutMs { get; set; } = 10_000;                    // Per-run wall-clock timeout forwarded to executor
    public int CompileTimeoutMs { get; set; } = 10_000;                // Per-compile wall-clock timeout forwarded to executor
    public int MaxOutputLength { get; set; } = 10_000;                 // Max chars of stdout/stderr returned to the client
}
