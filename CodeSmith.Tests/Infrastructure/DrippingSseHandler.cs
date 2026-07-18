// == Dripping SSE Stub Handler == //
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// HttpMessageHandler stub for streaming adapter tests that need control over chunk *timing*, not
/// just content: each scripted entry is delivered as one read after its delay, and an optional
/// trailing stall keeps the stream open (honouring cancellation) so idle- and total-timeout
/// behaviour can be exercised. The plain CapturingHttpHandler stays the fixture for everything
/// where timing is irrelevant.
/// </summary>
internal sealed class DrippingSseHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<(int DelayMs, string Chunk)> _script;
    private readonly int _stallMs;

    public DrippingSseHandler(IReadOnlyList<(int DelayMs, string Chunk)> script, int stallMs = 0)
    {
        _script  = script;
        _stallMs = stallMs;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ScriptedStream(_script, _stallMs))
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/event-stream");
        return Task.FromResult(response);
    }

    // == Scripted Read Stream == //

    private sealed class ScriptedStream : Stream
    {
        private readonly IReadOnlyList<(int DelayMs, string Chunk)> _script;
        private readonly int _stallMs;
        private int _index;

        public ScriptedStream(IReadOnlyList<(int DelayMs, string Chunk)> script, int stallMs)
        {
            _script  = script;
            _stallMs = stallMs;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_index < _script.Count)
            {
                var (delayMs, chunk) = _script[_index++];
                if (delayMs > 0) await Task.Delay(delayMs, ct);
                var bytes = Encoding.UTF8.GetBytes(chunk);
                bytes.CopyTo(buffer);
                return bytes.Length;
            }

            // Safety valve: stall (cancellably), then end the stream so a test can fail instead of hang
            if (_stallMs > 0) await Task.Delay(_stallMs, ct);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override bool CanRead  => true;
        public override bool CanSeek  => false;
        public override bool CanWrite => false;
        public override long Length   => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
