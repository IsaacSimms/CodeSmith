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

    private const string SimulationModel  = "claude-haiku-4-5-20251001"; // Fast/cheap — called once per test input, in parallel
    private const string EvaluationModel  = "claude-sonnet-4-6";         // Accurate — called once per test input, in parallel
    private const string GenerationModel  = "claude-sonnet-4-6";         // Quality generation — called once at session start
    private const int    ContextWindowSize = 200_000;                     // Token limit for all models used in this service

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

    public async Task<PromptLabSession> StartChallengeAsync(string challengeId, CancellationToken ct = default)
    {
        var challenge = GetChallenge(challengeId); // Validates the ID — throws ChallengeNotFoundException if invalid

        List<TestInput> testInputs;
        try
        {
            testInputs = await GenerateTestInputsAsync(challenge, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test input generation failed for {ChallengeId}; falling back to static inputs", challengeId);
            testInputs = challenge.TestInputs;
        }

        var session = new PromptLabSession { ChallengeId = challengeId, TestInputs = testInputs };
        _sessionStore.Set(session);

        _logger.LogInformation("Started session {SessionId} for {ChallengeId} with {Count} test inputs", session.SessionId, challengeId, testInputs.Count);
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

        // Use dynamically generated inputs if available, fall back to challenge's static inputs
        var testInputs = session.TestInputs.Count > 0 ? session.TestInputs : challenge.TestInputs;

        _logger.LogInformation("Processing attempt for session {SessionId}, challenge {ChallengeId}", sessionId, challenge.ChallengeId);

        try
        {
            // == Phase 1: Simulate (parallel Haiku calls) == //
            var (simulationOutputs, promptTokens) = await RunSimulationsAsync(challenge, testInputs, systemPromptContent, userMessageContent, ct);

            // == Phase 2: Evaluate (parallel Sonnet calls) == //
            var attempt = await EvaluateAttemptAsync(challenge, testInputs, systemPromptContent, userMessageContent, simulationOutputs, ct);
            attempt.PromptTokensUsed = promptTokens;
            attempt.ContextWindowSize = ContextWindowSize;

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

    private async Task<(List<(TestInput Input, string Output)> Outputs, int PromptTokens)> RunSimulationsAsync(
        Challenge challenge,
        List<TestInput> testInputs,
        string systemPromptContent,
        string userMessageContent,
        CancellationToken ct)
    {
        // Build the effective system prompt: locked base + user additions + hidden adversarial bias
        var effectiveSystemPrompt = BuildSimulationSystemPrompt(challenge, systemPromptContent);

        // Determine which field supplies the user message content for each test input
        bool userMessageIsEditable = challenge.EditableFields.Any(f => f.FieldType == Core.Enums.PromptFieldType.UserMessage);

        // Launch all test input simulations in parallel to minimise latency
        var tasks = testInputs.Select(input =>
        {
            var message = userMessageIsEditable
                ? BuildUserMessage(userMessageContent, input.UserMessage)
                : input.UserMessage;
            return SimulateOneInputAsync(input, effectiveSystemPrompt, message, ct);
        });

        var results = await Task.WhenAll(tasks);

        // All simulation calls share the same prompt — first result's input token count is representative
        var promptTokens = results.Length > 0 ? results[0].InputTokens : 0;
        return (results.Select(r => (r.Input, r.Output)).ToList(), promptTokens);
    }

    private async Task<(TestInput Input, string Output, int InputTokens)> SimulateOneInputAsync(
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
        return (input, output, (int)response.Usage.InputTokens);
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
        // Adversarial prompt comes before user additions so user instructions can override it
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

    // == Evaluation Phase == //

    private async Task<ChallengeAttempt> EvaluateAttemptAsync(
        Challenge challenge,
        List<TestInput> testInputs,
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
            MaxScore            = testInputs.Count * challenge.Rubric.Sum(r => r.MaxPoints),
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

    // == Test Input Generation == //

    private async Task<List<TestInput>> GenerateTestInputsAsync(Challenge challenge, CancellationToken ct)
    {
        // Pre-decide input 3 and 4 types server-side for a true 50/50 split
        var input3Type = Random.Shared.Next(2) == 0 ? "standard" : "edge case";
        var input4Type = Random.Shared.Next(2) == 0 ? "standard" : "edge case";

        var examplesJson = System.Text.Json.JsonSerializer.Serialize(
            challenge.TestInputs.Select(t => new { t.Label, t.UserMessage, t.ExpectedBehavior }));

        var prompt = $"""
            Generate exactly 4 test inputs for this prompt engineering challenge.

            Challenge: {challenge.Title}
            Category: {challenge.Category}
            Description: {challenge.Description.Trim()}
            Locked System Prompt (context only): {challenge.LockedSystemPrompt}

            Reference inputs (illustrate the domain and style — do NOT copy them):
            {examplesJson}

            Generation rules:
            - Input gen-1: standard — a typical, representative case for this challenge
            - Input gen-2: standard — another typical case with different subject matter from gen-1
            - Input gen-3: {input3Type}{(input3Type == "edge case" ? " — surprising or interesting angle or subject matter, but equally solvable by the same prompt technique as standard inputs" : " — a typical case with different subject matter from gen-1 and gen-2")}
            - Input gen-4: {input4Type}{(input4Type == "edge case" ? " — surprising or interesting angle or subject matter, but equally solvable by the same prompt technique as standard inputs" : " — a typical case with different subject matter from the other inputs")}

            All inputs must have distinct subject matter from each other and from the reference inputs.
            Edge cases should be surprising or interesting, NOT harder to solve — the same prompt engineering technique must work equally well.

            Return ONLY a valid JSON array of exactly 4 objects. Each object must have exactly these string fields:
            "inputId" (gen-1 through gen-4), "label" (2-4 word description), "userMessage" (the message to send), "expectedBehavior" (what a correct response must do).
            No preamble, no markdown fences, no explanation — JSON array only.
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model     = GenerationModel,
            MaxTokens = 600,
            System    = "You generate test inputs for prompt engineering challenges. Return only a valid JSON array as specified — no preamble.",
            Messages  = [new() { Role = Role.User, Content = prompt }]
        }, ct);

        var json  = ExtractJson(ExtractTextContent(response));
        var items = System.Text.Json.JsonSerializer.Deserialize<List<GeneratedTestInputDto>>(json)
            ?? throw new InvalidOperationException("Generation returned null JSON.");

        if (items.Count != 4)
            throw new InvalidOperationException($"Expected 4 generated inputs, got {items.Count}.");

        return items.Select(item => new TestInput
        {
            InputId         = item.InputId ?? $"gen-{items.IndexOf(item) + 1}",
            Label           = item.Label ?? "Unlabeled",
            UserMessage     = item.UserMessage ?? "",
            ExpectedBehavior = item.ExpectedBehavior ?? ""
        }).ToList();
    }

    // DTO for deserializing the generation response — not exposed outside this class
    private sealed class GeneratedTestInputDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("inputId")]
        public string? InputId { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("label")]
        public string? Label { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("userMessage")]
        public string? UserMessage { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("expectedBehavior")]
        public string? ExpectedBehavior { get; set; }
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
