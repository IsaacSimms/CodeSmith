// == Capturing HTTP Stub Handler == //
using System.Net;
using System.Text;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// HttpMessageHandler stub shared by the LLM adapter tests. Captures the last outgoing request
/// (URI + body) so tests can pin the request the SDK actually sends, and replays a canned response
/// built fresh per call so SDK-level retries never see a disposed HttpResponseMessage.
/// </summary>
internal sealed class CapturingHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly string _contentType;
    private readonly Exception? _exception;

    public HttpRequestMessage? LastRequest     { get; private set; }   // Most recent request message (URI, headers)
    public string?            LastRequestBody { get; private set; }   // Most recent request body, read before the SDK disposes it
    public int                CallCount       { get; private set; }   // Total requests seen (surfaces unexpected SDK retries)

    // contentType: "application/json" for regular completions, "text/event-stream" for SSE stream fixtures
    public CapturingHttpHandler(HttpStatusCode status, string body, string contentType = "application/json")
    {
        _status      = status;
        _body        = body;
        _contentType = contentType;
    }

    // Transport-failure overload — throws instead of responding, so adapters' connection-failure
    // paths can be exercised without a second stub handler type.
    public CapturingHttpHandler(Exception exception)
    {
        _exception   = exception;
        _status      = HttpStatusCode.OK;
        _body        = string.Empty;
        _contentType = "application/json";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest     = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();   // honour cancelled tokens like a real socket would

        if (_exception is not null) throw _exception;

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, _contentType)
        };
    }
}
