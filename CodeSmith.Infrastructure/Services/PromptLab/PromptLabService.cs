// == Prompt Lab Service == //
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services.PromptLab;

/// <summary>
/// Implementation of <see cref="IPromptLabService"/> that orchestrates three phases
/// per attempt: serving catalog data, running simulation (Haiku, parallel), and evaluating results (Sonnet).
/// </summary>
public class PromptLabService : IPromptLabService
{
    private readonly AnthropicClient _client;
    private readonly IPromptLabSessionStore _sessionStore;
    private readonly ILogger<PromptLabService> _logger;

    private const string SimulationModel = "claude-haiku-4-5-20251001"; // Fast/cheap — called once per test input, in parallel
    private const string EvaluationModel = "claude-sonnet-4-20250514";  // Accurate — called once per attempt

    public PromptLabService(
        IOptions<AnthropicOptions> options,
        IPromptLabSessionStore sessionStore,
        ILogger<PromptLabService> logger)
    {
        _client       = new AnthropicClient { ApiKey = options.Value.ApiKey };
        _sessionStore = sessionStore;
        _logger       = logger;
    }

    // == Catalog Operations (synchronous, in-memory) == //

    public IReadOnlyList<Challenge> GetChallenges() => ChallengeCatalog.All;

    public Challenge GetChallenge(string challengeId)
    {
        var challenge = ChallengeCatalog.All.FirstOrDefault(c => c.ChallengeId == challengeId);
        return challenge ?? throw new ChallengeNotFoundException(challengeId);
    }

    public PromptLabSession StartChallenge(string challengeId)
    {
        _ = GetChallenge(challengeId); // Validates the ID — throws ChallengeNotFoundException if invalid

        var session = new PromptLabSession { ChallengeId = challengeId };
        _sessionStore.Set(session);

        _logger.LogInformation("Started Prompt Lab session {SessionId} for challenge {ChallengeId}", session.SessionId, challengeId);
        return session;
    }

    // == Attempt Submission (async, 2 API phases) == //

    public async Task<ChallengeAttempt> SubmitAttemptAsync(
        Guid sessionId,
        string systemPromptContent,
        string userMessageContent,
        CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId)
            ?? throw new SessionNotFoundException(sessionId);

        var challenge = GetChallenge(session.ChallengeId);
        _logger.LogInformation("Processing attempt for session {SessionId}, challenge {ChallengeId}", sessionId, challenge.ChallengeId);

        try
        {
            // == Phase 1: Simulate (parallel Haiku calls) == //
            var simulationOutputs = await RunSimulationsAsync(challenge, systemPromptContent, userMessageContent, ct);

            // == Phase 2: Evaluate (single Sonnet call) == //
            var attempt = await EvaluateAttemptAsync(challenge, systemPromptContent, userMessageContent, simulationOutputs, ct);

            // == Persist result == //
            session.Attempts.Add(attempt);
            _sessionStore.Set(session);

            _logger.LogInformation("Attempt complete for session {SessionId}: {Score}/{Max}", sessionId, attempt.TotalScore, attempt.MaxScore);
            return attempt;
        }
        catch (Exception ex) when (ex is not AnthropicApiException and not SessionNotFoundException and not ChallengeNotFoundException)
        {
            _logger.LogError(ex, "Failed to process attempt for session {SessionId}", sessionId);
            throw new AnthropicApiException("Failed to evaluate prompt attempt. Please try again.", ex);
        }
    }

    // == Simulation Phase == //

    private async Task<List<(TestInput Input, string Output)>> RunSimulationsAsync(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        CancellationToken ct)
    {
        // Build the effective system prompt: locked base + user additions + hidden adversarial bias
        var effectiveSystemPrompt = BuildSimulationSystemPrompt(challenge, systemPromptContent);

        // Determine which field supplies the user message content for each test input
        bool userMessageIsEditable = challenge.EditableFields.Any(f => f.FieldType == Core.Enums.PromptFieldType.UserMessage);

        // Launch all test input simulations in parallel to minimise latency
        var tasks = challenge.TestInputs.Select(input =>
        {
            var message = userMessageIsEditable
                ? BuildUserMessage(userMessageContent, input.UserMessage)
                : input.UserMessage;
            return SimulateOneInputAsync(input, effectiveSystemPrompt, message, ct);
        });

        return [.. await Task.WhenAll(tasks)];
    }

    private async Task<(TestInput Input, string Output)> SimulateOneInputAsync(
        TestInput input,
        string systemPrompt,
        string userMessage,
        CancellationToken ct)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = SimulationModel,
            MaxTokens = 512,
            System    = systemPrompt,
            Messages  = [new() { Role = Role.User, Content = userMessage }]
        }, ct);

        var output = ExtractTextContent(response);
        _logger.LogDebug("Simulation output for input {InputId}: {Output}", input.InputId, output);
        return (input, output);
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

    private static string BuildSimulationSystemPrompt(Challenge challenge, string userSystemContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine(challenge.LockedSystemPrompt);
        if (!string.IsNullOrWhiteSpace(userSystemContent))
        {
            sb.AppendLine();
            sb.AppendLine(userSystemContent);
        }
        // Hidden adversarial prompt is appended last so it influences model behavior
        if (!string.IsNullOrWhiteSpace(challenge.HiddenAdversarialPrompt))
        {
            sb.AppendLine();
            sb.AppendLine(challenge.HiddenAdversarialPrompt);
        }
        return sb.ToString().Trim();
    }

    // == Evaluation Phase == //

    private async Task<ChallengeAttempt> EvaluateAttemptAsync(
        Challenge challenge,
        string systemPromptContent,
        string userMessageContent,
        List<(TestInput Input, string Output)> simulationOutputs,
        CancellationToken ct)
    {
        // Evaluate each test input in isolation (parallel) so outputs cannot contaminate each other's scores
        var resultTasks = simulationOutputs
            .Select(pair => EvaluateOneInputAsync(challenge, pair.Input, pair.Output, ct));

        var inputResults = await Task.WhenAll(resultTasks);

        var attempt = new ChallengeAttempt
        {
            SystemPromptContent = systemPromptContent,
            UserMessageContent  = userMessageContent,
            MaxScore            = challenge.TestInputs.Count * challenge.Rubric.Sum(r => r.MaxPoints),
            Results             = [.. inputResults],
        };

        attempt.TotalScore      = attempt.Results.Sum(r => r.CriterionScores.Sum(s => s.Points));
        attempt.OverallFeedback = BuildOverallFeedback(attempt);
        return attempt;
    }

    private async Task<TestInputResult> EvaluateOneInputAsync(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        CancellationToken ct)
    {
        var prompt = BuildSingleInputEvaluationPrompt(challenge, input, simulationOutput);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = EvaluationModel,
            MaxTokens = 512,
            System    = """
                You are an expert prompt engineering evaluator. Score this single model output against the rubric.
                You MUST respond with ONLY valid JSON matching this exact schema — no preamble, no explanation:
                {
                  "passed": true,
                  "criterionScores": [{ "criterionId": "string", "points": 0 }],
                  "feedback": "string"
                }
                """,
            Messages = [new() { Role = Role.User, Content = prompt }]
        }, ct);

        var json = ExtractTextContent(response);
        return ParseSingleInputResult(challenge, input, simulationOutput, json);
    }

    private static string BuildSingleInputEvaluationPrompt(Challenge challenge, TestInput input, string output)
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

    private static TestInputResult ParseSingleInputResult(
        Challenge challenge,
        TestInput input,
        string simulationOutput,
        string json)
    {
        try
        {
            var jsonText = ExtractJson(json);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var passed   = root.TryGetProperty("passed",   out var p) && p.GetBoolean();
            var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "";

            var criterionScores = new List<CriterionScore>();
            if (root.TryGetProperty("criterionScores", out var scoresEl))
            {
                foreach (var scoreEl in scoresEl.EnumerateArray())
                {
                    var criterionId = scoreEl.GetProperty("criterionId").GetString() ?? "";
                    var points      = scoreEl.GetProperty("points").GetInt32();
                    var criterion   = challenge.Rubric.FirstOrDefault(r => r.CriterionId == criterionId);

                    criterionScores.Add(new CriterionScore
                    {
                        CriterionId   = criterionId,
                        CriterionName = criterion?.Name ?? criterionId,
                        Points        = points,
                        MaxPoints     = criterion?.MaxPoints ?? 0
                    });
                }
            }

            return new TestInputResult
            {
                InputId          = input.InputId,
                Label            = input.Label,
                SimulationOutput = simulationOutput,
                Passed           = passed,
                CriterionScores  = criterionScores,
                Feedback         = feedback
            };
        }
        catch (JsonException)
        {
            return new TestInputResult
            {
                InputId          = input.InputId,
                Label            = input.Label,
                SimulationOutput = simulationOutput,
                Passed           = false,
                Feedback         = "Could not parse evaluation response."
            };
        }
    }

    private static string BuildOverallFeedback(ChallengeAttempt attempt)
    {
        var passed = attempt.Results.Count(r => r.Passed);
        var total  = attempt.Results.Count;
        var pct    = attempt.MaxScore > 0 ? attempt.TotalScore * 100 / attempt.MaxScore : 0;

        return passed == total
            ? $"All {total} test inputs passed ({attempt.TotalScore}/{attempt.MaxScore} pts). Excellent prompt engineering!"
            : $"{passed}/{total} test inputs passed ({pct}% of available points). Review the per-input feedback to refine your prompt.";
    }

    // == Helpers == //

    private static string ExtractTextContent(Message response)  // Extracts text content from an Anthropic message response
    {
        var texts = new List<string>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
                texts.Add(textBlock.Text);
        }
        return string.Join("", texts);
    }

    private static string ExtractJson(string text)  // Strips markdown code fences if the model wraps JSON despite instructions
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence    = trimmed.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }
}
