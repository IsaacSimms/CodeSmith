// == CodeSmith Telemetry Source == //
using System.Diagnostics;

namespace CodeSmith.Infrastructure.Diagnostics;

/// <summary>
/// The single ActivitySource for CodeSmith's custom spans. The LLM call path emits one
/// "llm.completion" root span per Completion with "usage.reserve" / "llm.call" / "usage.settle"
/// (or "usage.release") children, so provider time and enforcement time are separable in traces;
/// problem generation emits one "problem.generation.attempt" span per attempt so silent retries
/// are visible. Exported when the host wires OpenTelemetry to this source (see Program.cs);
/// without a listener, StartActivity returns null and the path costs nothing.
/// </summary>
public static class CodeSmithDiagnostics
{
    public const string SourceName = "CodeSmith";

    public static readonly ActivitySource Source = new(SourceName);
}
