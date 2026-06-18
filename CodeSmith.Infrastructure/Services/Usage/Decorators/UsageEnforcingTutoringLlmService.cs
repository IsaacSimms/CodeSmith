// == Usage Enforcing Decorator for ITutoringLlmService == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services.Usage.Decorators;

/// <summary>
/// Wraps the real ITutoringLlmService so that usage enforcement (check before, record after)
/// happens transparently for every call. The LLM adapter remains behind the seam.
/// </summary>
internal sealed class UsageEnforcingTutoringLlmService : ITutoringLlmService
{
    private readonly ITutoringLlmService _inner;
    private readonly ICurrentUser _currentUser;
    private readonly IUsageEnforcer _enforcer;
    private readonly ILlmPricing _pricing;
    private readonly AiProvider _provider;

    public UsageEnforcingTutoringLlmService(
        ITutoringLlmService inner,
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

    public async Task<LlmResponse> GenerateProblemAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        var objectId = RequireObjectId();
        var estInput = EstimateTokens(systemPrompt, userMessage);
        await _enforcer.CheckAndReserveAsync(objectId, _provider, estInput, maxTokens, ct);

        var response = await _inner.GenerateProblemAsync(systemPrompt, userMessage, maxTokens, ct);

        var cost = _pricing.ComputeCostUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);
        await _enforcer.RecordActualAsync(objectId, _provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed, cost, "Tutoring:ProblemGeneration", ct);

        return response;
    }

    public async Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default)
    {
        var objectId = RequireObjectId();
        var estInput = EstimateTokens(systemPrompt, history);
        await _enforcer.CheckAndReserveAsync(objectId, _provider, estInput, maxTokens, ct);

        var response = await _inner.GetGuidanceAsync(systemPrompt, history, maxTokens, ct);

        var cost = _pricing.ComputeCostUsd(_provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed);
        await _enforcer.RecordActualAsync(objectId, _provider, response.Model, response.InputTokensUsed, response.OutputTokensUsed, cost, "Tutoring:Guidance", ct);

        return response;
    }

    private string RequireObjectId()
    {
        var id = _currentUser.ObjectId;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Authenticated user (objectId) is required for usage enforcement.");
        return id;
    }

    private static int EstimateTokens(string systemPrompt, string userMessage)
        => (systemPrompt.Length + userMessage.Length) / 4 + 100;

    private static int EstimateTokens(string systemPrompt, IReadOnlyList<ChatMessage> history)
    {
        var total = systemPrompt.Length + 200;
        foreach (var m in history) total += m.Content.Length;
        return total / 4;
    }
}
