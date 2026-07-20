// == NDJSON Stream Envelope == //
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Streaming;

/// <summary>
/// The control-flow envelope every /stream endpoint runs inside. Owns the status-line-freeze
/// invariant of the chunk contract: failures before the first delta propagate to
/// AppExceptionHandler while the status line is still writable (real 402/404/429/502); once the
/// response has started, failures ride the stream as error events instead; caller cancellation
/// ends the stream silently (the client is gone — nothing to write, nobody to receive it).
/// Endpoints supply only their service call, receiving the writer for delta/reset sinks and
/// returning the final payload — the envelope writes the final event itself.
/// </summary>
public static class NdjsonStreamEnvelope
{
    public static async Task<IActionResult> RunAsync<TFinal>(
        HttpResponse response,
        Func<NdjsonStreamWriter, Task<TFinal>> streamBody,
        CancellationToken ct)
    {
        var writer = new NdjsonStreamWriter(response);
        try
        {
            var final = await streamBody(writer);
            await writer.WriteFinalAsync(final, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client gone — nothing to write and nobody to receive it
        }
        catch (Exception ex) when (response.HasStarted)
        {
            // Status line is frozen once deltas were written; the failure must ride the stream
            await writer.WriteErrorAsync(ex);
        }
        return new EmptyResult();   // body was written directly; nothing for MVC to execute
    }
}
