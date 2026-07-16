// == Prompt Simulation Phase == //
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.PromptLab;

// One test input's simulated output plus the token metadata the attempt surfaces to the UI
public record SimulatedInput(
    TestInput Input,
    string Output,
    int PromptTokens,
    int ContextWindowSize);

public interface IPromptSimulator
{
    // Per-input so the orchestrator can pipeline each input's simulate→evaluate chain —
    // wall clock becomes the slowest single chain instead of slowest-simulate + slowest-evaluate
    Task<SimulatedInput> SimulateOneAsync(
        Challenge challenge,
        TestInput input,
        string systemPromptContent,
        string userMessageContent,
        AiProvider provider,
        CancellationToken ct);
}

public sealed class PromptSimulator : IPromptSimulator
{
    private readonly ILlmServiceFactory _factory;
    private readonly ILogger<PromptSimulator> _logger;

    private const int SimulationMaxTokens = 512;

    public PromptSimulator(ILlmServiceFactory factory, ILogger<PromptSimulator> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // == SimulateOneAsync == //

    public async Task<SimulatedInput> SimulateOneAsync(
        Challenge challenge,
        TestInput input,
        string systemPromptContent,
        string userMessageContent,
        AiProvider provider,
        CancellationToken ct)
    {
        var effectiveSystemPrompt = BuildSimulationSystemPrompt(challenge, systemPromptContent);
        var userMessageIsEditable = challenge.EditableFields.Any(f => f.FieldType == PromptFieldType.UserMessage);

        var message = userMessageIsEditable
            ? TestInputMessage.Build(userMessageContent, input.UserMessage)
            : input.UserMessage;

        var response = await _factory.Get(provider).CompleteAsync(
            CompletionRequest.SingleTurn(effectiveSystemPrompt, message, ModelTier.Fast, SimulationMaxTokens, "PromptLab:Simulate"), ct);

        _logger.LogDebug("Simulation output for input {InputId}: {Output}", input.InputId, response.Content);
        return new SimulatedInput(input, response.Content, response.InputTokensUsed, response.ContextWindowSize);
    }

    // == Prompt Builders == //

    // Combines locked base, optional adversarial inject, and user additions into the effective system prompt.
    // Adversarial content precedes user additions so the user's instructions can override it.
    private static string BuildSimulationSystemPrompt(Challenge challenge, string userSystemContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine(challenge.LockedSystemPrompt);

        if (!string.IsNullOrWhiteSpace(challenge.HiddenAdversarialPrompt))
        {
            sb.AppendLine();
            sb.AppendLine(challenge.HiddenAdversarialPrompt);
        }

        if (!string.IsNullOrWhiteSpace(userSystemContent))
        {
            sb.AppendLine();
            sb.AppendLine(userSystemContent);
        }

        return sb.ToString().Trim();
    }
}
