// == Activity Capture Test Helper == //
using System.Diagnostics;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// Captures completed activities from the CodeSmith ActivitySource so tests can assert on the
/// custom spans the LLM call path emits. Dispose to detach the listener. Test classes that use
/// this helper should share the "CodeSmithTelemetry" xUnit collection — listeners are
/// process-global, so parallel span-emitting tests would otherwise capture each other's spans.
/// </summary>
public sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _stopped = [];

    public ActivityCapture(string sourceName = "CodeSmith")
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (_stopped) _stopped.Add(activity); }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public IReadOnlyList<Activity> Stopped { get { lock (_stopped) return _stopped.ToArray(); } }

    // Single completed span by name, asserting exactly one was captured
    public Activity Single(string name)
    {
        var matches = Stopped.Where(a => a.OperationName == name).ToList();
        Assert.Single(matches);
        return matches[0];
    }

    public IReadOnlyList<Activity> All(string name)
        => Stopped.Where(a => a.OperationName == name).ToList();

    public void Dispose() => _listener.Dispose();
}
