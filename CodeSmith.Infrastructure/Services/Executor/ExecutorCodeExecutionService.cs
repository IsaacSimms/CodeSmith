// == Executor Code Execution Service == //
using System.Net.Http.Json;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Executor;

/// <summary>
/// Executes user-submitted code by calling the CodeSmith.Executor image hosted as an
/// ordinary Azure Container App that scales to zero. Unlike the Dynamic Sessions Adapter
/// there is no pool, no session identifier, and no bearer token — the executor is reachable
/// only on internal ingress inside the Container Apps Environment, which is the trust boundary.
/// </summary>
public class ExecutorCodeExecutionService : ICodeExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExecutorOptions _options;
    private readonly ILogger<ExecutorCodeExecutionService> _logger;

    public ExecutorCodeExecutionService(
        IHttpClientFactory httpClientFactory,
        IOptions<CodeExecutionOptions> options,
        ILogger<ExecutorCodeExecutionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Executor;
        _logger = logger;
    }

    // == Execute User Code == //
    public async Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken ct = default)
    {
        // SessionId is unused here (stateless HTTP per execute); only Dynamic Sessions needs it as a pool identifier.
        if (!ExecutorLanguageMap.TryGet(request.Language, out var languageKey))
            throw new CodeExecutionException($"Language '{request.Language}' is not supported by the executor.");

        var body = new ExecutorExecuteRequest
        {
            Language = languageKey,
            Code = request.Code,
            RunTimeoutMs = _options.RunTimeoutMs,
            CompileTimeoutMs = _options.CompileTimeoutMs
        };

        ExecutorExecuteResponse? response;
        try
        {
            var httpClient = _httpClientFactory.CreateClient(ExecutorHttpClient.Name);
            var httpResponse = await httpClient.PostAsJsonAsync(BuildExecutePath(), body, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errBody = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Executor returned {StatusCode} for {Language}: {Body}",
                    (int)httpResponse.StatusCode, request.Language, errBody);
                throw new CodeExecutionException(
                    $"Executor rejected the request ({(int)httpResponse.StatusCode}): {errBody}");
            }

            response = await httpResponse.Content.ReadFromJsonAsync<ExecutorExecuteResponse>(cancellationToken: ct);
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
            _logger.LogError(ex, "Executor request failed for {Language}", request.Language);
            throw new CodeExecutionException(
                "Code sandbox unavailable. The executor may be scaling up from zero or misconfigured.", ex);
        }

        if (response is null)
            throw new CodeExecutionException("Executor returned an empty response.");

        return new CodeExecutionResult
        {
            Stdout = Truncate(response.Stdout),
            Stderr = Truncate(response.Stderr),
            ExitCode = response.TimedOut ? -1 : response.ExitCode,
            TimedOut = response.TimedOut
        };
    }

    // == Helpers == //
    // Relative URI so BaseAddress (the executor's internal ingress FQDN) is applied by HttpClient.
    private string BuildExecutePath()
    {
        var executePath = string.IsNullOrWhiteSpace(_options.ExecutePath) ? "/execute" : _options.ExecutePath;
        return executePath.StartsWith('/') ? executePath : "/" + executePath;
    }

    private string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxOutputLength) return value;
        return value[.._options.MaxOutputLength] + "\n[output truncated]";
    }
}
