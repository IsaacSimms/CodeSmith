// == NDJSON Endpoint Test Harness == //
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace CodeSmith.Tests.Api;

/// <summary>
/// Test plumbing for the /stream endpoints: a DefaultHttpContext whose Response.HasStarted flips on
/// the first body write (the default test feature always reports false, which would break the
/// mid-stream-error contract under test) plus an NDJSON reader for asserting the event lines.
/// </summary>
internal static class NdjsonEndpointHarness
{
    public static (DefaultHttpContext Context, MemoryStream Body) CreateStreamingContext()
    {
        var body    = new TrackingStream();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartTrackingResponseFeature(body));
        context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));
        return (context, body);
    }

    // One parsed JsonDocument root per NDJSON line, in write order
    public static IReadOnlyList<JsonElement> ReadEvents(MemoryStream body)
        => Encoding.UTF8.GetString(body.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToList();

    // == Start-Tracking Plumbing == //

    private sealed class TrackingStream : MemoryStream
    {
        public bool Written { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Written = true;
            base.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            Written = true;
            return base.WriteAsync(buffer, offset, count, ct);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            Written = true;
            return base.WriteAsync(buffer, ct);
        }
    }

    private sealed class StartTrackingResponseFeature(TrackingStream body) : HttpResponseFeature
    {
        public override bool HasStarted => body.Written;
    }
}
