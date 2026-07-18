// == Usage Enforcing Decorator for ILlmService == //
using System.Diagnostics;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.Usage;
using CodeSmith.Infrastructure.Diagnostics;

namespace CodeSmith.Infrastructure.Services.Usage.Decorators;

/// <summary>
/// Wraps the real <see cref="ILlmService"/> so usage enforcement runs transparently for every completion,
/// regardless of feature or model tier. The raw LLM adapter stays behind the seam. The decorator drives
/// the enforcer's reserve → settle / release lifecycle: hold an upper-bound estimate before the call,
/// reconcile to actuals after a success, and refund the hold if the call fails. Because the hold is
/// persisted at reserve time, concurrent completions for one user (the Prompt Lab fan-out) cannot all
/// pass the same gate. The caller's intent (<see cref="CompletionRequest.Feature"/>) drives the ledger
/// entry's feature tag.
/// </summary>
internal sealed class UsageEnforcingLlmService : ILlmService
{
    private readonly ILlmService _inner;
    private readonly ICurrentUser _currentUser;
    private readonly IUsageEnforcer _enforcer;
    private readonly ILlmPricing _pricing;
    private readonly AiProvider _provider;

    public UsageEnforcingLlmService(
        ILlmService inner,
        ICurrentUser currentUser,
        IUsageEnforcer enforcer,
        ILlmPricing pricing,
        AiProvider provider)
    {
        _inner = inner;
        _currentUser = currentUser;
        _enforcer = enforcer;
        _pricing = pricing;
        _provider = provider;
    }

    public async Task<LlmResponse> CompleteAsync(CompletionRequest request, CancellationToken ct = default)
    {
        var objectId = RequireObjectId();
        var clientIp = _currentUser.ClientIp;

        // One root span per Completion; reserve / call / settle children split enforcement time
        // from provider time in traces, which is the whole point of instrumenting this path.
        using var completion = CodeSmithDiagnostics.Source.StartActivity("llm.completion");
        completion?.SetTag("codesmith.provider", _provider.ToString());
        completion?.SetTag("codesmith.tier", request.Tier.ToString());
        completion?.SetTag("codesmith.feature", request.Feature);

        var estInput = EstimateInputTokens(request);

        UsageReservation reservation;
        using (CodeSmithDiagnostics.Source.StartActivity("usage.reserve"))
        {
            reservation = await _enforcer.ReserveAsync(objectId, clientIp, _provider, estInput, request.MaxTokens, ct);
        }

        LlmResponse response;
        try
        {
            using var call = CodeSmithDiagnostics.Source.StartActivity("llm.call");
            response = await _inner.CompleteAsync(EffectiveRequest(request, reservation.UsedFree), ct);
            call?.SetTag("codesmith.model", response.Model);
            call?.SetTag("codesmith.tokens.input", response.InputTokensUsed);
            call?.SetTag("codesmith.tokens.output", response.OutputTokensUsed);
            call?.SetTag("codesmith.was_truncated", response.WasTruncated);
        }
        catch (Exception ex)
        {
            completion?.SetStatus(ActivityStatusCode.Error, ex.Message);

            // The call produced nothing billable — refund the hold so it consumes no quota, then propagate.
            using (CodeSmithDiagnostics.Source.StartActivity("usage.release"))
            {
                await _enforcer.ReleaseAsync(reservation, ct);
            }
            throw;
        }

        // Raw provider cost (for the ledger / margin) and the customer charge (debited from credits).
        var providerCost = _pricing.ComputeCostUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);
        var charge       = _pricing.ComputeChargeUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);

        // Settle reconciles the upper-bound hold to actual usage; if settling itself fails after a real
        // spend, the conservative hold simply stands (we do not refund a call that happened).
        using (CodeSmithDiagnostics.Source.StartActivity("usage.settle"))
        {
            await _enforcer.SettleAsync(reservation, response.Model, response.InputTokensUsed, response.OutputTokensUsed, charge, providerCost, request.Feature, ct);
        }

        return response;
    }

    // == StreamAsync == //

    public Task<LlmResponse> StreamAsync(CompletionRequest request, Func<string, CancellationToken, Task> onDelta, CancellationToken ct = default)
        => throw new NotImplementedException();

    private string RequireObjectId()
    {
        var id = _currentUser.ObjectId;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Authenticated user (objectId) is required for usage enforcement.");
        return id;
    }

    // Downgrades evaluations to the Fast tier only while consuming free quota (inside the 48h window).
    // Paid / post-window usage keeps Accurate for quality.
    private static CompletionRequest EffectiveRequest(CompletionRequest request, bool usedFree)
    {
        if (!usedFree || !IsEvaluationFeature(request.Feature))
            return request;

        return new CompletionRequest
        {
            SystemPrompt = request.SystemPrompt,
            Messages = request.Messages,
            Tier = ModelTier.Fast,
            MaxTokens = request.MaxTokens,
            Feature = request.Feature
        };
    }

    // Rough upper-bound estimate of input tokens (~4 chars/token) plus fixed overhead for system framing.
    // Used only for the pre-call quota gate; actual tokens from the response drive the recorded cost.
    private static int EstimateInputTokens(CompletionRequest request)
    {
        var chars = request.SystemPrompt.Length;
        foreach (var m in request.Messages) chars += m.Content.Length;
        return chars / 4 + 100;
    }

    private static bool IsEvaluationFeature(string? feature)
    {
        if (string.IsNullOrWhiteSpace(feature)) return false;
        return feature.Contains("Evaluate", StringComparison.OrdinalIgnoreCase) ||
               feature.Contains("SystemLab", StringComparison.OrdinalIgnoreCase);
    }
}
