// == Prompt Evaluation Phase == //
using System.Text;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.PromptLab;

public interface IPromptEvaluator
{
    Task<ChallengeAttempt> EvaluateAsync(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        SimulationResult simulation,
        AiProvider provider,
        CancellationToken ct);
}

public sealed class PromptEvaluator : IPromptEvaluator
{
    private readonly ILlmServiceFactory _factory;
    private readonly ILogger<PromptEvaluator> _logger;

    private const int EvaluationMaxTokens = 512;

    public PromptEvaluator(ILlmServiceFactory factory, ILogger<PromptEvaluator> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // == EvaluateAsync == //

    public async Task<ChallengeAttempt> EvaluateAsync(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        SimulationResult simulation,
        AiProvider provider,
        CancellationToken ct)
    {
        var userMessageIsEditable = challenge.EditableFields.Any(f => f.FieldType == PromptFieldType.UserMessage);

        // Evaluate each test input in isolation (parallel) so outputs cannot contaminate each other's scores
        var resultTasks = simulation.Outputs.Select(pair =>
        {
            var computedUserMessage = userMessageIsEditable
                ? TestInputMessage.Build(userMessageContent, pair.Input.UserMessage)
                : pair.Input.UserMessage;
            return EvaluateOneAsync(challenge, pair.Input, pair.Output, computedUserMessage, provider, ct);
        });

        var inputResults = await Task.WhenAll(resultTasks);

        var attempt = new ChallengeAttempt
        {
            SystemPromptContent = systemPromptContent,
            UserMessageContent  = userMessageContent,
            MaxScore            = simulation.Outputs.Count * challenge.Rubric.Sum(r => r.MaxPoints),
            Results             = [.. inputResults],
            AdversarialHint     = challenge.HiddenAdversarialPrompt ?? "",
        };

        attempt.TotalScore      = attempt.Results.Sum(r => r.CriterionScores.Sum(s => s.Points));
        attempt.OverallFeedback = BuildOverallFeedback(attempt);
        return attempt;
    }

    private async Task<TestInputResult> EvaluateOneAsync(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        string computedUserMessage,
        AiProvider provider,
        CancellationToken ct)
    {
        var systemPrompt = """
            You are an expert prompt engineering evaluator. Score this single model output against the rubric.
            You MUST respond with ONLY valid JSON matching this exact schema — no preamble, no explanation:
            {
              "passed": true,
              "criterionScores": [{ "criterionId": "string", "points": 0 }],
              "feedback": "string"
            }
            """;
        var prompt = BuildEvaluationPrompt(challenge, input, simulationOutput);

        var response = await _factory.Get(provider).CompleteAsync(
            CompletionRequest.SingleTurn(systemPrompt, prompt, ModelTier.Accurate, EvaluationMaxTokens, "PromptLab:Evaluate"), ct);

        return ParseResult(challenge, input, simulationOutput, computedUserMessage, response.Content);
    }

    // == Prompt Builders == //

    private static string BuildEvaluationPrompt(Challenge challenge, TestInput input, string output)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Challenge: {challenge.Title}");
        sb.AppendLine($"Description: {challenge.Description}");
        sb.AppendLine();

        sb.AppendLine("Rubric Criteria:");
        foreach (var criterion in challenge.Rubric)
            sb.AppendLine($"  - [{criterion.CriterionId}] {criterion.Name} (max {criterion.MaxPoints} pts): {criterion.Description}");

        sb.AppendLine();
        sb.AppendLine($"Test Input: {input.Label}");
        sb.AppendLine($"Expected behavior: {input.ExpectedBehavior}");
        sb.AppendLine($"Actual model output:");
        sb.AppendLine(output);
        sb.AppendLine();
        sb.AppendLine("Score this output against ALL rubric criteria. It 'passes' if it scores full points on all criteria.");
        sb.AppendLine("Return JSON only.");
        return sb.ToString();
    }

    // == Result Parsing == //

    private static TestInputResult ParseResult(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        string computedUserMessage,
        string json)
    {
        try
        {
            // LlmJson owns fence-stripping, malformed-JSON failure, and rubric integrity (skip/clamp/round)
            using var doc = LlmJson.Parse(json);
            var root = doc.RootElement;

            var passed   = root.TryGetProperty("passed",   out var p) && p.GetBoolean();
            var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "";

            return new TestInputResult
            {
                InputId          = input.InputId,
                Label            = input.Label,
                UserMessage      = computedUserMessage,
                SimulationOutput = simulationOutput,
                Passed           = passed,
                CriterionScores  = LlmJson.ParseCriterionScores(challenge.Rubric, root),
                Feedback         = feedback
            };
        }
        catch (EvaluationParseException)
        {
            return new TestInputResult
            {
                InputId          = input.InputId,
                Label            = input.Label,
                UserMessage      = computedUserMessage,
                SimulationOutput = simulationOutput,
                Passed           = false,
                Feedback         = "Could not parse evaluation response."
            };
        }
    }

    // == Feedback == //

    private static string BuildOverallFeedback(ChallengeAttempt attempt)
    {
        var passed = attempt.Results.Count(r => r.Passed);
        var total  = attempt.Results.Count;
        var pct    = attempt.MaxScore > 0 ? attempt.TotalScore * 100 / attempt.MaxScore : 0;

        return passed == total
            ? $"All {total} test inputs passed ({attempt.TotalScore}/{attempt.MaxScore} pts). Excellent prompt engineering!"
            : $"{passed}/{total} test inputs passed ({pct}% of available points). Review the per-input feedback to refine your prompt.";
    }
}
