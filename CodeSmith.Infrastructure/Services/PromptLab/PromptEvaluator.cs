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
    // Per-input so the orchestrator can pipeline each input's simulate→evaluate chain; inputs are
    // still scored in isolation so outputs cannot contaminate each other's scores
    Task<TestInputResult> EvaluateOneAsync(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        string userMessageContent,
        AiProvider provider,
        CancellationToken ct);

    // Pure aggregation of the per-input results into the scored attempt (totals + overall feedback)
    ChallengeAttempt AssembleAttempt(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        IReadOnlyList<TestInputResult> results);
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

    // == EvaluateOneAsync == //

    public async Task<TestInputResult> EvaluateOneAsync(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        string userMessageContent,
        AiProvider provider,
        CancellationToken ct)
    {
        // Recompute the effective user message the same way simulation did (shared TestInputMessage),
        // so the result records exactly what the scored output was generated from.
        var userMessageIsEditable = challenge.EditableFields.Any(f => f.FieldType == PromptFieldType.UserMessage);
        var computedUserMessage   = userMessageIsEditable
            ? TestInputMessage.Build(userMessageContent, input.UserMessage)
            : input.UserMessage;

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

    // == AssembleAttempt == //

    public ChallengeAttempt AssembleAttempt(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        IReadOnlyList<TestInputResult> results)
    {
        var attempt = new ChallengeAttempt
        {
            SystemPromptContent = systemPromptContent,
            UserMessageContent  = userMessageContent,
            MaxScore            = results.Count * challenge.Rubric.Sum(r => r.MaxPoints),
            Results             = [.. results],
            AdversarialHint     = challenge.HiddenAdversarialPrompt ?? "",
        };

        attempt.TotalScore      = attempt.Results.Sum(r => r.CriterionScores.Sum(s => s.Points));
        attempt.OverallFeedback = BuildOverallFeedback(attempt);
        return attempt;
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
