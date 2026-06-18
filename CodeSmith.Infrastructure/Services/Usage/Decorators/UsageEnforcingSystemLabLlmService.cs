// == Usage Enforcing Decorator for ISystemLabLlmService == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services.Usage.Decorators;

internal sealed class UsageEnforcingSystemLabLlmService : ISystemLabLlmService
{
    private readonly ISystemLabLlmService _inner;
    private readonly ICurrentUser _currentUser;
    private readonly IUsageEnforcer _enforcer;
    private readonly ILlmPricing _pricing;
    private readonly AiProvider _provider;

    public UsageEnforcingSystemLabLlmService(
        ISystemLabLlmService inner,
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

    public async Task<LlmResponse> EvaluateJustificationAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        var oid = RequireObjectId();
        var est = Estimate(systemPrompt, userMessage);
        await _enforcer.CheckAndReserveAsync(oid, _provider, est, maxTokens, ct);

        var r = await _inner.EvaluateJustificationAsync(systemPrompt, userMessage, maxTokens, ct);
        var cost = _pricing.ComputeCostUsd(_provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed);
        await _enforcer.RecordActualAsync(oid, _provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed, cost, "SystemLab:Evaluate", ct);
        return r;
    }

    public async Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default)
    {
        var oid = RequireObjectId();
        var est = Estimate(systemPrompt, history);
        await _enforcer.CheckAndReserveAsync(oid, _provider, est, maxTokens, ct);

        var r = await _inner.GetGuidanceAsync(systemPrompt, history, maxTokens, ct);
        var cost = _pricing.ComputeCostUsd(_provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed);
        await _enforcer.RecordActualAsync(oid, _provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed, cost, "SystemLab:Chat", ct);
        return r;
    }

    private string RequireObjectId() => _currentUser.ObjectId ?? throw new InvalidOperationException("objectId required for enforcement");

    private static int Estimate(string s, string u) => (s.Length + u.Length) / 4 + 50;
    private static int Estimate(string s, IReadOnlyList<ChatMessage> h) => (s.Length + h.Sum(m => m.Content.Length)) / 4 + 100;
}
