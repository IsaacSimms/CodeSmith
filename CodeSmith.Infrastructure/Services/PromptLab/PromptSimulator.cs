// == Prompt Simulation Phase == //
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.PromptLab;

public record SimulationResult(
    List<(TestInput Input, string Output)> Outputs,
    int PromptTokens,
    int ContextWindowSize);

public interface IPromptSimulator
{
    Task<SimulationResult> SimulateAsync(
        Challenge challenge,
        List<TestInput> testInputs,
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

    // == SimulateAsync == //

    public async Task<SimulationResult> SimulateAsync(
        Challenge challenge,
        List<TestInput> testInputs,
        string systemPromptContent,
        string userMessageContent,
        AiProvider provider,
        CancellationToken ct)
    {
        var effectiveSystemPrompt  = BuildSimulationSystemPrompt(challenge, systemPromptContent);
        var userMessageIsEditable  = challenge.EditableFields.Any(f => f.FieldType == PromptFieldType.UserMessage);

        // Launch all test input simulations in parallel to minimise latency
        var tasks = testInputs.Select(input =>
        {
            var message = userMessageIsEditable
                ? BuildUserMessage(userMessageContent, input.UserMessage)
                : input.UserMessage;
            return SimulateOneAsync(input, effectiveSystemPrompt, message, provider, ct);
        });

        var results = await Task.WhenAll(tasks);

        // All simulation calls share the same prompt — first result's token count is representative
        var promptTokens      = results.Length > 0 ? results[0].InputTokens      : 0;
        var contextWindowSize = results.Length > 0 ? results[0].ContextWindowSize : 0;

        return new SimulationResult(
            results.Select(r => (r.Input, r.Output)).ToList(),
            promptTokens,
            contextWindowSize);
    }

    private async Task<(TestInput Input, string Output, int InputTokens, int ContextWindowSize)> SimulateOneAsync(
        TestInput input,
        string systemPrompt,
        string userMessage,
        AiProvider provider,
        CancellationToken ct)
    {
        var response = await _factory.Get(provider).CompleteAsync(
            CompletionRequest.SingleTurn(systemPrompt, userMessage, ModelTier.Fast, SimulationMaxTokens, "PromptLab:Simulate"), ct);

        _logger.LogDebug("Simulation output for input {InputId}: {Output}", input.InputId, response.Content);
        return (input, response.Content, response.InputTokensUsed, response.ContextWindowSize);
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

    // Substitutes {input} in the user's template with the test input value.
    // If the template contains no placeholder, the test input value is appended on a new line.
    private static string BuildUserMessage(string template, string testInputValue)
    {
        const string placeholder = "{input}";
        return template.Contains(placeholder, StringComparison.OrdinalIgnoreCase)
            ? template.Replace(placeholder, testInputValue, StringComparison.OrdinalIgnoreCase)
            : $"{template}\n\n{testInputValue}";
    }
}
