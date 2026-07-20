// == NDJSON Stream Envelope Tests == //
using CodeSmith.Api.Streaming;
using CodeSmith.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Tests.Api;

/// <summary>
/// Pins the control-flow envelope shared by every /stream endpoint: final event written from the
/// body's return value, mid-stream failures ride the stream as error events once the status line is
/// frozen, pre-stream failures propagate for real HTTP status mapping, and caller cancellation ends
/// the stream silently.
/// </summary>
public class NdjsonStreamEnvelopeTests
{
    [Fact]
    public async Task RunAsync_BodySucceeds_WritesFinalEventAndReturnsEmptyResult()
    {
        var (context, body) = NdjsonEndpointHarness.CreateStreamingContext();

        var result = await NdjsonStreamEnvelope.RunAsync(context.Response, async writer =>
        {
            await writer.WriteDeltaAsync("Hel", CancellationToken.None);
            await writer.WriteDeltaAsync("lo!", CancellationToken.None);
            return new { answer = "Hello!" };
        }, CancellationToken.None);

        Assert.IsType<EmptyResult>(result);
        Assert.Equal("application/x-ndjson", context.Response.ContentType);
        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Equal(3, events.Count);
        Assert.Equal("delta",  events[0].GetProperty("type").GetString());
        Assert.Equal("final",  events[2].GetProperty("type").GetString());
        Assert.Equal("Hello!", events[2].GetProperty("data").GetProperty("answer").GetString());
    }

    [Fact]
    public async Task RunAsync_MidStreamFailure_WritesErrorEventWithMappedStatusCode()
    {
        // The status line froze at the first delta, so the 502 must ride the stream as an error event
        var (context, body) = NdjsonEndpointHarness.CreateStreamingContext();

        var result = await NdjsonStreamEnvelope.RunAsync<object>(context.Response, async writer =>
        {
            await writer.WriteDeltaAsync("part", CancellationToken.None);
            throw new AiServiceException("provider fell over");
        }, CancellationToken.None);

        Assert.IsType<EmptyResult>(result);
        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Equal("delta", events[0].GetProperty("type").GetString());
        Assert.Equal("error", events[^1].GetProperty("type").GetString());
        Assert.Equal(502,     events[^1].GetProperty("code").GetInt32());
    }

    [Fact]
    public async Task RunAsync_PreStreamFailure_PropagatesAndWritesNothing()
    {
        // Nothing was written yet, so the exception must reach AppExceptionHandler for a real status
        var (context, body) = NdjsonEndpointHarness.CreateStreamingContext();
        var sessionId = Guid.NewGuid();

        await Assert.ThrowsAsync<SessionNotFoundException>(() =>
            NdjsonStreamEnvelope.RunAsync<object>(context.Response,
                _ => throw new SessionNotFoundException(sessionId),
                CancellationToken.None));

        Assert.Empty(NdjsonEndpointHarness.ReadEvents(body));
    }

    [Fact]
    public async Task RunAsync_CallerCancellation_EndsStreamSilently()
    {
        // Client gone: no error event, no final event, no exception out of the envelope
        var (context, body) = NdjsonEndpointHarness.CreateStreamingContext();
        using var cts = new CancellationTokenSource();

        var result = await NdjsonStreamEnvelope.RunAsync<object>(context.Response, async writer =>
        {
            await writer.WriteDeltaAsync("part", CancellationToken.None);
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        }, cts.Token);

        Assert.IsType<EmptyResult>(result);
        var events = NdjsonEndpointHarness.ReadEvents(body);
        Assert.Single(events);
        Assert.Equal("delta", events[0].GetProperty("type").GetString());
    }
}
