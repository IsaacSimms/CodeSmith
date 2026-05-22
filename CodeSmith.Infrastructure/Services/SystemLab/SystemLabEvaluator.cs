// == System Lab Evaluation Phase == //
using System.Text;
using System.Text.Json;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.SystemLab;

public interface ISystemLabEvaluator
{
    Task<ScenarioAttempt> EvaluateAsync(Scenario scenario, string justification, CancellationToken ct);
}

public sealed class SystemLabEvaluator : ISystemLabEvaluator
{
    private readonly ISystemLabLlmService _llmService;
    private readonly ILogger<SystemLabEvaluator> _logger;

    private const int EvaluationMaxTokens = 1500;

    public SystemLabEvaluator(ISystemLabLlmService llmService, ILogger<SystemLabEvaluator> logger)
    {
        _llmService = llmService;
        _logger     = logger;
    }

    // == EvaluateAsync == //

    public async Task<ScenarioAttempt> EvaluateAsync(Scenario scenario, string justification, CancellationToken ct)
    {
        var systemPrompt = BuildEvaluatorSystemPrompt(scenario);
        var userMessage  = BuildEvaluationPrompt(scenario, justification);

        var response = await _llmService.EvaluateJustificationAsync(systemPrompt, userMessage, EvaluationMaxTokens, ct);

        return ParseAttempt(scenario, justification, response.Content);
    }

    // == Prompt Builders == //

    private static string BuildEvaluatorSystemPrompt(Scenario scenario)
    {
        var modeInstruction = scenario.EvaluationMode switch
        {
            Core.Enums.EvaluationMode.SingleAnswer =>
                "There is one correct answer. Score for technical correctness — award points only when the student identifies the right approach and explains why.",
            Core.Enums.EvaluationMode.TradeoffReasoning =>
                "There is no single correct answer, but one approach is clearly better given the stated constraints. Score for whether the student correctly identifies the constraint that determines the better choice and explains the tradeoff explicitly.",
            Core.Enums.EvaluationMode.OpenJudgment =>
                "Multiple valid designs exist. Do NOT score based on which design the student chose. Score exclusively on the quality of their reasoning process: did they surface relevant tradeoffs, state their assumptions, and reason through consequences?",
            _ => ""
        };

        const string jsonSchema = """
            {
              "criterionScores": [{ "criterionId": "string", "points": 0 }],
              "tradeoffResults": [{ "tradeoffQuestion": "string", "engaged": true, "feedback": "string" }],
              "securityDeduction": 0,
              "securityFeedback": null,
              "overallFeedback": "string"
            }
            """;

        return $"""
            You are an expert infrastructure and platform engineering evaluator.
            Score the student's justification against the rubric and required tradeoffs provided.
            {modeInstruction}

            CRITICAL: For each required tradeoff, 'engaged' means the student demonstrated genuine causal reasoning —
            they explained WHY a tradeoff exists given the scenario constraints, not merely mentioned the relevant terms.
            A response that restates the tradeoff question or lists keywords without causal reasoning scores as NOT engaged.

            For security deduction: apply a deduction ONLY if the student's proposed design actively introduces or
            explicitly endorses one of the listed security pitfalls. Do not deduct for omission of security discussion
            unless the omission itself constitutes endorsing an insecure design.

            You MUST respond with ONLY valid JSON matching this exact schema — no preamble, no explanation:
            {jsonSchema}
            """;
    }

    private static string BuildEvaluationPrompt(Scenario scenario, string justification)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Scenario: {scenario.Title}");
        sb.AppendLine($"Category: {scenario.Category}");
        sb.AppendLine($"Difficulty: {scenario.Difficulty}");
        sb.AppendLine();
        sb.AppendLine("Description:");
        sb.AppendLine(scenario.Description.Trim());
        sb.AppendLine();
        sb.AppendLine("Constraints:");
        sb.AppendLine(scenario.Constraints.Trim());
        sb.AppendLine();

        sb.AppendLine("Rubric Criteria:");
        foreach (var criterion in scenario.Rubric)
            sb.AppendLine($"  - [{criterion.CriterionId}] {criterion.Name} (max {criterion.MaxPoints} pts): {criterion.Description}");
        sb.AppendLine();

        sb.AppendLine("Required Tradeoffs (the student was shown these questions):");
        for (var i = 0; i < scenario.RequiredTradeoffs.Count; i++)
            sb.AppendLine($"  {i + 1}. {scenario.RequiredTradeoffs[i]}");
        sb.AppendLine();

        sb.AppendLine("Security Pitfalls to check (NOT shown to student — apply deduction only if the student's design endorses one):");
        foreach (var pitfall in scenario.SecurityPitfalls)
            sb.AppendLine($"  - {pitfall}");
        sb.AppendLine($"Maximum security deduction available: {scenario.MaxSecurityDeduction} points");
        sb.AppendLine();

        sb.AppendLine("Student's Justification:");
        sb.AppendLine(justification);
        sb.AppendLine();
        sb.AppendLine("Score against ALL rubric criteria and ALL required tradeoffs. Return JSON only.");
        return sb.ToString();
    }

    // == Result Parsing == //

    private ScenarioAttempt ParseAttempt(Scenario scenario, string justification, string json)
    {
        try
        {
            var jsonText = ExtractJson(json);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var criterionScores  = ParseCriterionScores(scenario, root);
            var tradeoffResults  = ParseTradeoffResults(scenario, root);
            var securityDeduction = root.TryGetProperty("securityDeduction", out var sd) ? sd.GetInt32() : 0;
            var securityFeedback  = root.TryGetProperty("securityFeedback",  out var sf) && sf.ValueKind != JsonValueKind.Null
                ? sf.GetString()
                : null;
            var overallFeedback   = root.TryGetProperty("overallFeedback", out var of) ? of.GetString() ?? "" : "";

            // Clamp deduction to MaxSecurityDeduction to prevent evaluator over-penalizing
            securityDeduction = Math.Clamp(securityDeduction, 0, scenario.MaxSecurityDeduction);

            var rubricScore    = criterionScores.Sum(s => s.Points);
            var maxRubricScore = scenario.Rubric.Sum(r => r.MaxPoints);
            var totalScore     = Math.Max(0, rubricScore - securityDeduction);

            return new ScenarioAttempt
            {
                JustificationContent = justification,
                CriterionScores      = criterionScores,
                TradeoffResults      = tradeoffResults,
                RubricScore          = rubricScore,
                MaxRubricScore       = maxRubricScore,
                SecurityDeduction    = securityDeduction,
                SecurityFeedback     = securityDeduction > 0 ? securityFeedback : null,
                TotalScore           = totalScore,
                MaxScore             = maxRubricScore,
                OverallFeedback      = overallFeedback
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse evaluator response JSON");
            return new ScenarioAttempt
            {
                JustificationContent = justification,
                OverallFeedback      = "Could not parse evaluation response. Please try again."
            };
        }
    }

    private static List<CriterionScore> ParseCriterionScores(Scenario scenario, JsonElement root)
    {
        var scores = new List<CriterionScore>();
        if (!root.TryGetProperty("criterionScores", out var scoresEl)) return scores;

        foreach (var el in scoresEl.EnumerateArray())
        {
            var criterionId = el.GetProperty("criterionId").GetString() ?? "";
            var points      = el.GetProperty("points").GetInt32();
            var criterion   = scenario.Rubric.FirstOrDefault(r => r.CriterionId == criterionId);

            scores.Add(new CriterionScore
            {
                CriterionId   = criterionId,
                CriterionName = criterion?.Name ?? criterionId,
                Points        = Math.Clamp(points, 0, criterion?.MaxPoints ?? points),
                MaxPoints     = criterion?.MaxPoints ?? 0
            });
        }
        return scores;
    }

    private static List<TradeoffResult> ParseTradeoffResults(Scenario scenario, JsonElement root)
    {
        var results = new List<TradeoffResult>();
        if (!root.TryGetProperty("tradeoffResults", out var tradeoffsEl)) return results;

        var tradeoffList = scenario.RequiredTradeoffs.ToList();
        var index        = 0;

        foreach (var el in tradeoffsEl.EnumerateArray())
        {
            // Prefer evaluator-echoed question; fall back to authored question by position
            var question = el.TryGetProperty("tradeoffQuestion", out var tq) ? tq.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(question) && index < tradeoffList.Count)
                question = tradeoffList[index];

            var engaged  = el.TryGetProperty("engaged",  out var eng) && eng.GetBoolean();
            var feedback = el.TryGetProperty("feedback", out var fb)  ? fb.GetString() ?? "" : "";

            results.Add(new TradeoffResult { TradeoffQuestion = question, Engaged = engaged, Feedback = feedback });
            index++;
        }
        return results;
    }

    // == Helpers == //

    private static string ExtractJson(string text) // Strips markdown code fences if the model wraps JSON despite instructions
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
