// == Dynamic Sessions Code Execution Service == //
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.DynamicSessions;

/// <summary>
/// Executes user-submitted code via Azure Container Apps custom Dynamic Sessions.
/// Routes to a multi-language executor container inside a Hyper-V isolated session;
/// reuses request.SessionId as the pool identifier for warm multi-run within a tutoring session.
/// </summary>
public class DynamicSessionsCodeExecutionService : ICodeExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDynamicSessionsTokenProvider _tokenProvider;
    private readonly DynamicSessionsOptions _options;
    private readonly ILogger<DynamicSessionsCodeExecutionService> _logger;

    public DynamicSessionsCodeExecutionService(
        IHttpClientFactory httpClientFactory,
        IDynamicSessionsTokenProvider tokenProvider,
        IOptions<CodeExecutionOptions> options,
        ILogger<DynamicSessionsCodeExecutionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _options = options.Value.DynamicSessions;
        _logger = logger;
    }

    // == Execute User Code == //
    public async Task<CodeExecutionResult> ExecuteAsync(CodeExecutionRequest request, CancellationToken ct = default)
    {
        if (request.SessionId is null || request.SessionId == Guid.Empty)
            throw new CodeExecutionException(
                "DynamicSessions requires CodeExecutionRequest.SessionId (tutoring session id) to allocate a sandbox.");

        if (!LanguageMap.TryGetValue(request.Language, out var languageKey))
            throw new CodeExecutionException($"Language '{request.Language}' is not supported by DynamicSessions.");

        var identifier = request.SessionId.Value.ToString("D"); // guid format is pool-identifier safe
        var path = BuildExecutePath(identifier);

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
            var token = await _tokenProvider.GetAccessTokenAsync(ct);
            var httpClient = _httpClientFactory.CreateClient(DynamicSessionsHttpClient.Name);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var httpResponse = await httpClient.SendAsync(httpRequest, ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errBody = await httpResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "DynamicSessions returned {StatusCode} for {Language} session {SessionId}: {Body}",
                    (int)httpResponse.StatusCode, request.Language, identifier, errBody);
                throw new CodeExecutionException(
                    $"DynamicSessions rejected the request ({(int)httpResponse.StatusCode}): {errBody}");
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
            _logger.LogError(ex, "DynamicSessions request failed for {Language} session {SessionId}",
                request.Language, identifier);
            throw new CodeExecutionException(
                "Code sandbox unavailable. The session pool may be cold or misconfigured.", ex);
        }

        if (response is null)
            throw new CodeExecutionException("DynamicSessions returned an empty response.");

        return new CodeExecutionResult
        {
            Stdout = Truncate(response.Stdout),
            Stderr = Truncate(response.Stderr),
            ExitCode = response.TimedOut ? -1 : response.ExitCode,
            TimedOut = response.TimedOut
        };
    }

    // == Helpers == //
    private string BuildExecutePath(string identifier)
    {
        var executePath = string.IsNullOrWhiteSpace(_options.ExecutePath) ? "/execute" : _options.ExecutePath;
        if (!executePath.StartsWith('/'))
            executePath = "/" + executePath;

        // Relative URI so BaseAddress (pool management endpoint) is applied by HttpClient.
        return $"{executePath}?identifier={Uri.EscapeDataString(identifier)}";
    }

    private string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= _options.MaxOutputLength) return value;
        return value[.._options.MaxOutputLength] + "\n[output truncated]";
    }

    // Maps CodeSmith Language enum to executor language keys (stable contract with CodeSmith.Executor).
    private static readonly Dictionary<Language, string> LanguageMap = new()
    {
        [Language.Python]     = "python",
        [Language.TypeScript] = "typescript",
        [Language.Go]         = "go",
        [Language.Cpp]        = "cpp",
        [Language.Rust]       = "rust",
        [Language.Java]       = "java",
        [Language.CSharp]     = "csharp",
    };
}
