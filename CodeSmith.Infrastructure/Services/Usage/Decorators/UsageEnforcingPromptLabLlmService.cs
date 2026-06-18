// == Usage Enforcing Decorator for IPromptLabLlmService == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;

namespace CodeSmith.Infrastructure.Services.Usage.Decorators;

internal sealed class UsageEnforcingPromptLabLlmService : IPromptLabLlmService
{
    private readonly IPromptLabLlmService _inner;
    private readonly ICurrentUser _currentUser;
    private readonly IUsageEnforcer _enforcer;
    private readonly ILlmPricing _pricing;
    private readonly AiProvider _provider;

    public UsageEnforcingPromptLabLlmService(
        IPromptLabLlmService inner,
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

    public async Task<LlmResponse> SimulatePromptAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        await Check(systemPrompt, userMessage, maxTokens, ct);
        var r = await _inner.SimulatePromptAsync(systemPrompt, userMessage, maxTokens, ct);
        await Record(r, "PromptLab:Simulate", ct);
        return r;
    }

    public async Task<LlmResponse> EvaluateResponseAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        await Check(systemPrompt, userMessage, maxTokens, ct);
        var r = await _inner.EvaluateResponseAsync(systemPrompt, userMessage, maxTokens, ct);
        await Record(r, "PromptLab:Evaluate", ct);
        return r;
    }

    public async Task<LlmResponse> GenerateTestInputsAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken ct = default)
    {
        await Check(systemPrompt, userMessage, maxTokens, ct);
        var r = await _inner.GenerateTestInputsAsync(systemPrompt, userMessage, maxTokens, ct);
        await Record(r, "PromptLab:TestInputGeneration", ct);
        return r;
    }

    public async Task<LlmResponse> GetGuidanceAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, int maxTokens, CancellationToken ct = default)
    {
        var objectId = RequireObjectId();
        var est = Estimate(systemPrompt, history);
        await _enforcer.CheckAndReserveAsync(objectId, _provider, est, maxTokens, ct);

        var r = await _inner.GetGuidanceAsync(systemPrompt, history, maxTokens, ct);
        var cost = _pricing.ComputeCostUsd(_provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed);
        await _enforcer.RecordActualAsync(objectId, _provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed, cost, "PromptLab:Chat", ct);
        return r;
    }

    private async Task Check(string sys, string user, int max, CancellationToken ct)
    {
        var oid = RequireObjectId();
        await _enforcer.CheckAndReserveAsync(oid, _provider, Estimate(sys, user), max, ct);
    }

    private async Task Record(LlmResponse r, string feature, CancellationToken ct)
    {
        var oid = RequireObjectId();
        var cost = _pricing.ComputeCostUsd(_provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed);
        await _enforcer.RecordActualAsync(oid, _provider, r.Model, r.InputTokensUsed, r.OutputTokensUsed, cost, feature, ct);
    }

    private string RequireObjectId() => _currentUser.ObjectId ?? throw new InvalidOperationException("objectId required");

    private static int Estimate(string s, string u) => (s.Length + u.Length) / 4 + 50;
    private static int Estimate(string s, IReadOnlyList<ChatMessage> h) => (s.Length + h.Sum(m => m.Content.Length)) / 4 + 100;
}
