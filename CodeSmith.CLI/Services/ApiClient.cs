// == API Client Service == //
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models;

namespace CodeSmith.CLI.Services;

/// <summary>
/// HTTP client wrapper for communicating with the CodeSmith API.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // == Create Session == //

    public async Task<ProblemSession> CreateSessionAsync(Difficulty difficulty, Language language, CancellationToken ct = default)  // Creates a new coding problem session at the specified difficulty and language
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/session",
            new { difficulty = difficulty.ToString(), language = language.ToString() },
            ct);

        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<ProblemSession>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize session response.");
    }

    // == Send Chat Message == //

    public async Task<string> SendChatAsync(Guid sessionId, string message, CancellationToken ct = default)  // Sends a chat message within an existing session and returns the assistant's response
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/session/{sessionId}/chat",
            new { message },
            ct);

        await EnsureSuccessAsync(response, ct);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize chat response.");

        return chatResponse.Response;
    }

    // == Error Handling == //

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"API returned {(int)response.StatusCode}: {body}");
        }
    }
}
