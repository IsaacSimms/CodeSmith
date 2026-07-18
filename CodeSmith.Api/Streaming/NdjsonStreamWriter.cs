// == NDJSON Stream Writer == //
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSmith.Api.Middleware;

namespace CodeSmith.Api.Streaming;

/// <summary>
/// Writes the streaming chunk contract shared by every /stream endpoint: one JSON object per line,
/// flushed per event so chunks leave the server immediately. Event types — delta (text), reset
/// (clear text shown for an abandoned generation attempt), final (the same payload the blocking
/// sibling endpoint returns, under "data"), error (mid-stream failure; carries the status code the
/// request would have had, since the real status line is frozen once the first delta was written).
/// </summary>
public sealed class NdjsonStreamWriter
{
    // Matches the app's controller JSON: camelCase + enums as strings
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpResponse _response;

    public NdjsonStreamWriter(HttpResponse response)
    {
        _response = response;
        _response.ContentType = "application/x-ndjson";
    }

    public Task WriteDeltaAsync(string text, CancellationToken ct)
        => WriteEventAsync(new { type = "delta", text }, ct);

    public Task WriteResetAsync(CancellationToken ct)
        => WriteEventAsync(new { type = "reset" }, ct);

    public Task WriteFinalAsync<T>(T data, CancellationToken ct)
        => WriteEventAsync(new { type = "final", data }, ct);

    // Best-effort by design: the client may already be gone when a mid-stream failure is reported
    public async Task WriteErrorAsync(Exception exception)
    {
        var (status, _, detail) = AppExceptionHandler.Map(exception);
        try
        {
            await WriteEventAsync(new { type = "error", code = status, message = detail }, CancellationToken.None);
        }
        catch (Exception)
        {
            // Connection is dead — nothing left to tell anyone
        }
    }

    private async Task WriteEventAsync(object payload, CancellationToken ct)
    {
        await _response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions) + "\n", ct);
        await _response.Body.FlushAsync(ct);
    }
}
