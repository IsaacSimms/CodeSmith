// == Usage Enforcing Decorator for ILlmService == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services.Usage.Decorators;

/// <summary>
/// Wraps the real <see cref="ILlmService"/> so usage enforcement (check before, record after) happens
/// transparently for every completion, regardless of feature or model tier. The raw LLM adapter
/// stays behind the seam. Because the seam is now a single operation, one decorator replaces the
/// former per-capability trio. The caller's intent (<see cref="CompletionRequest.Feature"/>) drives
/// the ledger entry's feature tag.
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

        var estInput = EstimateInputTokens(request);
        var usedFree = await _enforcer.CheckAndReserveAsync(objectId, clientIp, _provider, estInput, request.MaxTokens, ct);

        // Downgrade evaluations to Fast tier only while consuming free quota (inside 48h window).
        // Paid / post-window usage gets Accurate for quality.
        var effectiveRequest = request;
        if (usedFree && IsEvaluationFeature(request.Feature))
        {
            effectiveRequest = new CompletionRequest
            {
                SystemPrompt = request.SystemPrompt,
                Messages = request.Messages,
                Tier = ModelTier.Fast,
                MaxTokens = request.MaxTokens,
                Feature = request.Feature
            };
        }

        var response = await _inner.CompleteAsync(effectiveRequest, ct);

        // Raw provider cost (for the ledger / margin) and the customer charge (debited from credits).
        var providerCost = _pricing.ComputeCostUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);
        var charge       = _pricing.ComputeChargeUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);
        await _enforcer.RecordActualAsync(objectId, clientIp, _provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed, charge, providerCost, request.Feature, ct);

        return response;
    }

    private string RequireObjectId()
    {
        var id = _currentUser.ObjectId;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Authenticated user (objectId) is required for usage enforcement.");
        return id;
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
