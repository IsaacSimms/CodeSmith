// == LLM Service Interface == //
using CodeSmith.Core.Models;

namespace CodeSmith.Core.Interfaces;

/// <summary>
/// The single, provider-agnostic LLM seam. Two operation shapes over the same completion:
/// CompleteAsync returns the full response in one piece; StreamAsync pushes text deltas through
/// onDelta as the provider produces them and still returns the same final LlmResponse (real token
/// counts, truncation flag) once the stream ends — so callers that meter usage observe identical
/// post-conditions on both shapes. Caller intent (tier, feature) travels on the request.
/// Implementations are stateless and map the tier to a concrete model internally.
/// </summary>
public interface ILlmService
{
    Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default);

    // Streams the completion: onDelta is awaited per text delta (in order, never concurrently);
    // the returned LlmResponse carries the full concatenated content plus final usage metadata.
    Task<LlmResponse> StreamAsync(
        CompletionRequest request,
        Func<string, CancellationToken, Task> onDelta,
        CancellationToken ct = default);
}
