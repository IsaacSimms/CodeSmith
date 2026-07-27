// == CodeSmith Executor HTTP Contracts == //
using System.Text.Json.Serialization;

namespace CodeSmith.Infrastructure.Services.Executor;

/// <summary>
/// Request/response DTOs for the CodeSmith.Executor POST /execute surface.
/// Kept in lockstep with CodeSmith.Executor and shared by every Adapter that speaks
/// to that image — direct HTTP (Container App) and Dynamic Sessions (pooled + token).
/// </summary>
internal sealed class ExecutorExecuteRequest
{
    [JsonPropertyName("language")] public string Language { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("runTimeoutMs")] public int RunTimeoutMs { get; set; }
    [JsonPropertyName("compileTimeoutMs")] public int CompileTimeoutMs { get; set; }
}

internal sealed class ExecutorExecuteResponse
{
    [JsonPropertyName("stdout")] public string Stdout { get; set; } = string.Empty;
    [JsonPropertyName("stderr")] public string Stderr { get; set; } = string.Empty;
    [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
    [JsonPropertyName("timedOut")] public bool TimedOut { get; set; }
}

/// <summary>
/// Shared name for the direct-HTTP executor HttpClient registered via IHttpClientFactory.
/// </summary>
public static class ExecutorHttpClient
{
    public const string Name = "Executor";
}
