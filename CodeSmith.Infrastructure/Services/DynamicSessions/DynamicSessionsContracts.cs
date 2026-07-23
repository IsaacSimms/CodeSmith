// == Dynamic Sessions Executor HTTP Contracts == //
using System.Text.Json.Serialization;

namespace CodeSmith.Infrastructure.Services.DynamicSessions;

/// <summary>
/// Request/response DTOs for the custom session container POST /execute surface.
/// Kept in lockstep with CodeSmith.Executor.
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
/// Shared name for the Dynamic Sessions HttpClient registered via IHttpClientFactory.
/// </summary>
public static class DynamicSessionsHttpClient
{
    public const string Name = "DynamicSessions";
}
