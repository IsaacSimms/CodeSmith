// == Piston Code Execution Service == //
using System.Net.Http.Json;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Piston;

/// <summary>
/// Executes user-submitted code by delegating to a Piston sandbox
/// (https://github.com/engineer-man/piston) running as a local Docker
/// container. Each submission runs in an isolated Linux sandbox with no
/// network access, a chroot filesystem, and cgroup CPU/memory/time limits.
/// </summary>
public class PistonCodeExecutionService : ICodeExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPistonRuntimeResolver _runtimeResolver;
    private readonly PistonOptions _options;
    private readonly ILogger<PistonCodeExecutionService> _logger;

    public PistonCodeExecutionService(
        IHttpClientFactory httpClientFactory,
        IPistonRuntimeResolver runtimeResolver,
        IOptions<CodeExecutionOptions> options,
        ILogger<PistonCodeExecutionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _runtimeResolver = runtimeResolver;
        _options = options.Value.Piston;
        _logger = logger;
    }

    // == Execute User Code == //
    public async Task<CodeExecutionResult> ExecuteAsync(Language language, string code, CancellationToken ct = default)
    {
        if (!PistonLanguageMap.TryGet(language, out var mapping))
            throw new CodeExecutionException($"Language '{language}' is not mapped to a Piston runtime.");

        // Piston's /api/v2/execute requires an exact installed version; it does NOT accept "*".
        var version = await _runtimeResolver.ResolveVersionAsync(mapping.Language, ct);

        var request = new PistonExecuteRequest
        {
            Language = mapping.Language,
            Version = version,
            Files = new List<PistonFile> { new() { Name = mapping.FileName, Content = code } },
            RunTimeout = _options.RunTimeoutMs,
            CompileTimeout = _options.CompileTimeoutMs
        };

        PistonExecuteResponse? response;
        try
        {
            var httpClient = _httpClientFactory.CreateClient(PistonHttpClient.Name);
            var httpResponse = await httpClient.PostAsJsonAsync("/api/v2/execute", request, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                // Surface Piston's actual error message (e.g. "Requested runtime is unknown")
                // instead of a generic 'Piston unavailable' hiding the real problem.
                var body = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Piston returned {StatusCode} for {Language} (version={Version}): {Body}",
                    (int)httpResponse.StatusCode, language, version, body);
                throw new CodeExecutionException(
                    $"Piston rejected the request ({(int)httpResponse.StatusCode}): {body}");
            }

            response = await httpResponse.Content.ReadFromJsonAsync<PistonExecuteResponse>(cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (CodeExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Piston request failed for {Language}", language);
            throw new CodeExecutionException(
                "Piston unavailable. Is the container running? See README for setup instructions.", ex);
        }

        if (response is null)
            throw new CodeExecutionException("Piston returned an empty response.");

        // Compile failure short-circuits: return compile stage output and skip run mapping.
        if (response.Compile is { Code: not (null or 0) } compile)
        {
            return new CodeExecutionResult
            {
                Stdout = Truncate(compile.Stdout),
                Stderr = Truncate(compile.Stderr),
                ExitCode = compile.Code ?? -1,
                TimedOut = IsTimeout(compile.Signal)
            };
        }

        var run = response.Run;
        var timedOut = IsTimeout(run.Signal);

        return new CodeExecutionResult
        {
            Stdout = Truncate(run.Stdout),
            Stderr = Truncate(timedOut && string.IsNullOrEmpty(run.Stderr)
                ? $"Process killed: execution exceeded {_options.RunTimeoutMs / 1000} second timeout."
                : run.Stderr),
            ExitCode = timedOut ? -1 : run.Code ?? -1,
            TimedOut = timedOut
        };
    }

    // == Helpers == //
    private static bool IsTimeout(string? signal) =>
        string.Equals(signal, "SIGKILL", StringComparison.Ordinal)
        || string.Equals(signal, "SIGTERM", StringComparison.Ordinal);

    private string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxOutputLength) return value;
        return value[.._options.MaxOutputLength] + "\n[output truncated]";
    }
}
