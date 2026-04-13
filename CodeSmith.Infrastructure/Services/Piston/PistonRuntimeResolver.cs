// == Piston Runtime Resolver == //
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CodeSmith.Core.Exceptions;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.Piston;

/// <summary>
/// Singleton cache of the language → installed-version map returned by
/// Piston's GET /api/v2/runtimes. Populated lazily on the first call and
/// held for the application lifetime. Thread-safe.
/// </summary>
public class PistonRuntimeResolver : IPistonRuntimeResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PistonOptions _options;
    private readonly ILogger<PistonRuntimeResolver> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Dictionary<string, string>? _cache;

    public PistonRuntimeResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<CodeExecutionOptions> options,
        ILogger<PistonRuntimeResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value.Piston;
        _logger = logger;
    }

    public async Task<string> ResolveVersionAsync(string pistonLanguage, CancellationToken ct = default)
    {
        var cache = await GetOrLoadAsync(ct);

        if (cache.TryGetValue(pistonLanguage, out var version))
            return version;

        throw new CodeExecutionException(
            $"No Piston runtime installed for '{pistonLanguage}'. Install it first via " +
            $"POST {_options.BaseUrl}/api/v2/packages. See README for the full install snippet.");
    }

    private async Task<Dictionary<string, string>> GetOrLoadAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        await _loadLock.WaitAsync(ct);
        try
        {
            if (_cache is not null) return _cache;

            var client = _httpClientFactory.CreateClient(PistonHttpClient.Name);
            List<PistonRuntimeInfo>? runtimes;
            try
            {
                runtimes = await client.GetFromJsonAsync<List<PistonRuntimeInfo>>("/api/v2/runtimes", ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Piston runtimes");
                throw new CodeExecutionException(
                    "Could not reach Piston to list installed runtimes. Is the container running? See README.", ex);
            }

            // Piston returns a primary `language` name plus optional `aliases` (e.g. csharp has
            // aliases ["mono","c#",...]). Index by both so callers can resolve via whichever
            // name the language map uses. First write wins if two runtimes share an alias.
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var runtime in runtimes ?? new List<PistonRuntimeInfo>())
            {
                _cache.TryAdd(runtime.Language, runtime.Version);
                foreach (var alias in runtime.Aliases ?? new List<string>())
                    _cache.TryAdd(alias, runtime.Version);
            }

            _logger.LogInformation("Loaded {Count} Piston runtimes", _cache.Count);
            return _cache;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private sealed class PistonRuntimeInfo
    {
        [JsonPropertyName("language")] public string Language { get; set; } = "";
        [JsonPropertyName("version")]  public string Version { get; set; } = "";
        [JsonPropertyName("aliases")]  public List<string>? Aliases { get; set; }
    }
}

/// <summary>
/// Shared name for the Piston HttpClient registered via IHttpClientFactory.
/// </summary>
public static class PistonHttpClient
{
    public const string Name = "Piston";
}
