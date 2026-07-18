// == Stream Timeout Guard == //
namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Combines caller cancellation with the two streaming timeouts — idle-between-events (covers
/// time-to-first-token) and total stream duration — into a single token the adapters hand to
/// their SDK stream. Pulse() re-arms the idle timer as each event arrives. A guard-initiated
/// cancellation surfaces as an OperationCanceledException whose token is NOT the caller's, so the
/// adapters' existing wrap filter converts it to AiServiceException instead of letting a provider
/// stall masquerade as caller cancellation (499 vs 502 in the API mapping).
/// </summary>
internal sealed class StreamGuard : IDisposable
{
    private readonly CancellationTokenSource _idle;
    private readonly CancellationTokenSource _total;
    private readonly CancellationTokenSource _combined;
    private readonly TimeSpan _idleTimeout;

    public StreamGuard(CancellationToken caller, TimeSpan idleTimeout, TimeSpan totalTimeout)
    {
        _idleTimeout = idleTimeout;
        _idle        = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _total       = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _combined    = CancellationTokenSource.CreateLinkedTokenSource(_idle.Token, _total.Token);

        _idle.CancelAfter(idleTimeout);
        _total.CancelAfter(totalTimeout);
    }

    public CancellationToken Token => _combined.Token;

    public void Pulse() => _idle.CancelAfter(_idleTimeout);   // Stream progress observed — restart the idle countdown

    public void Dispose()
    {
        _combined.Dispose();
        _idle.Dispose();
        _total.Dispose();
    }
}
