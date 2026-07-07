// == System Lab Evaluation Phase == //
using System.Text;
using System.Text.Json;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Infrastructure.Services.SystemLab;

public interface ISystemLabEvaluator
{
    Task<ScenarioAttempt> EvaluateAsync(Scenario scenario, string justification, AiProvider provider, CancellationToken ct);
}

public sealed class SystemLabEvaluator : ISystemLabEvaluator
{
    private readonly ILlmServiceFactory _factory;

    private const int EvaluationMaxTokens = 1500;

    public SystemLabEvaluator(ILlmServiceFactory factory)
    {
        _factory = factory;
    }

    // == EvaluateAsync == //

    public async Task<ScenarioAttempt> EvaluateAsync(Scenario scenario, string justification, AiProvider provider, CancellationToken ct)
    {
        var systemPrompt = BuildEvaluatorSystemPrompt(scenario);
        var userMessage  = BuildEvaluationPrompt(scenario, justification);

        var response = await _factory.Get(provider).CompleteAsync(
            CompletionRequest.SingleTurn(systemPrompt, userMessage, ModelTier.Accurate, EvaluationMaxTokens, "SystemLab:Evaluate"), ct);

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

        // Build the dimensionDeductions schema dynamically from the scenario's dimensions
        var dimensionsSchema = scenario.Dimensions.Count > 0
            ? string.Join(",\n    ", scenario.Dimensions.Select(d =>
                $"{{ \"dimensionName\": \"{d.Name}\", \"deduction\": 0, \"feedback\": null }}"))
            : "";

        var jsonSchema = $$"""
            {
              "criterionScores": [{ "criterionId": "string", "points": 0 }],
              "tradeoffResults": [{ "tradeoffQuestion": "string", "engaged": true, "feedback": "string" }],
              "dimensionDeductions": [
                {{dimensionsSchema}}
              ],
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

            For each cross-cutting dimension deduction: apply a deduction ONLY if the student's proposed design
            actively introduces or explicitly endorses one of the listed pitfalls for that dimension. Do not deduct
            for omission of discussion unless the omission itself constitutes endorsing a bad design.

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

        if (scenario.Dimensions.Count > 0)
        {
            sb.AppendLine("Cross-Cutting Dimensions to evaluate (NOT shown to student — apply deduction only if the student's design endorses a pitfall):");
            foreach (var dim in scenario.Dimensions)
            {
                sb.AppendLine($"  [{dim.Name}] (max deduction: {dim.MaxDeduction} pts)");
                foreach (var pitfall in dim.Pitfalls)
                    sb.AppendLine($"    - {pitfall}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Student's Justification:");
        sb.AppendLine(justification);
        sb.AppendLine();
        sb.AppendLine("Score against ALL rubric criteria and ALL required tradeoffs. Return JSON only.");
        return sb.ToString();
    }

    // == Result Parsing == //

    private static ScenarioAttempt ParseAttempt(Scenario scenario, string justification, string json)
    {
        // LlmJson owns fence-stripping, malformed-JSON failure (EvaluationParseException), and rubric integrity
        using var doc = LlmJson.Parse(json);
        var root = doc.RootElement;

        var criterionScores     = LlmJson.ParseCriterionScores(scenario.Rubric, root);
        var tradeoffResults     = ParseTradeoffResults(scenario, root);
        var dimensionDeductions = ParseDimensionDeductions(scenario, root);
        var overallFeedback     = root.TryGetProperty("overallFeedback", out var of) ? of.GetString() ?? "" : "";

        var rubricScore     = criterionScores.Sum(s => s.Points);
        var maxRubricScore  = scenario.Rubric.Sum(r => r.MaxPoints);
        var totalDeductions = dimensionDeductions.Sum(d => d.Deduction);
        var totalScore      = Math.Max(0, rubricScore - totalDeductions);

        return new ScenarioAttempt
        {
            JustificationContent = justification,
            CriterionScores      = criterionScores,
            TradeoffResults      = tradeoffResults,
            DimensionDeductions  = dimensionDeductions,
            RubricScore          = rubricScore,
            MaxRubricScore       = maxRubricScore,
            TotalScore           = totalScore,
            MaxScore             = maxRubricScore,
            OverallFeedback      = overallFeedback
        };
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

    private static List<DimensionDeduction> ParseDimensionDeductions(Scenario scenario, JsonElement root)
    {
        var results = new List<DimensionDeduction>();
        if (!root.TryGetProperty("dimensionDeductions", out var deductionsEl)) return results;

        foreach (var el in deductionsEl.EnumerateArray())
        {
            var name      = el.TryGetProperty("dimensionName", out var n) ? n.GetString() ?? "" : "";
            var deduction = el.TryGetProperty("deduction", out var d)
                ? (int)Math.Round(d.GetDouble())
                : 0;
            var feedback  = el.TryGetProperty("feedback",      out var f) && f.ValueKind != JsonValueKind.Null
                ? f.GetString()
                : null;

            // Skip hallucinated dimension names, then clamp to the dimension's MaxDeduction —
            // the evaluator can neither invent penalties nor over-penalize real dimensions
            var dim = scenario.Dimensions.FirstOrDefault(x => x.Name == name);
            if (dim is null) continue;
            deduction = Math.Clamp(deduction, 0, dim.MaxDeduction);

            results.Add(new DimensionDeduction
            {
                DimensionName = name,
                Deduction     = deduction,
                Feedback      = deduction > 0 ? feedback : null
            });
        }
        return results;
    }
}
