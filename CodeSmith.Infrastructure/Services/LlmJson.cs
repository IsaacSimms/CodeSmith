// == LLM JSON Parsing Module == //
using System.Text.Json;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Defensive parsing of Completion content that is expected to be JSON. Owns the quirks of
/// model output (markdown fences, hallucinated rubric IDs, out-of-range or fractional scores)
/// so every consumer shares one parse strategy and one failure mode: EvaluationParseException.
/// </summary>
internal static class LlmJson
{
    // == ExtractJson == //

    public static string ExtractJson(string text) // Strips markdown code fences if the model wraps JSON despite instructions
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

    // == Parse == //

    public static JsonDocument Parse(string text)
    {
        try
        {
            return JsonDocument.Parse(ExtractJson(text));
        }
        catch (JsonException ex)
        {
            throw new EvaluationParseException("LLM returned malformed JSON", ex);
        }
    }

    // == Deserialize == //

    public static T Deserialize<T>(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ExtractJson(text))
                ?? throw new EvaluationParseException("LLM returned null JSON");
        }
        catch (JsonException ex)
        {
            throw new EvaluationParseException("LLM returned malformed JSON", ex);
        }
    }

    // == ParseCriterionScores == //

    // The one rubric-integrity walk: entries without a criterionId or with an ID not in the rubric
    // are dropped (no phantom points), points tolerate fractional/missing values, and every score
    // is clamped to [0, MaxPoints].
    public static List<CriterionScore> ParseCriterionScores(IReadOnlyList<RubricCriterion> rubric, JsonElement root)
    {
        var scores = new List<CriterionScore>();
        if (!root.TryGetProperty("criterionScores", out var scoresEl)) return scores;

        foreach (var el in scoresEl.EnumerateArray())
        {
            if (!el.TryGetProperty("criterionId", out var cidEl)) continue;
            var criterionId = cidEl.GetString() ?? "";
            var criterion   = rubric.FirstOrDefault(r => r.CriterionId == criterionId);
            if (criterion is null) continue; // Skip hallucinated criterion IDs — prevents phantom points inflating the score

            var points = el.TryGetProperty("points", out var ptsEl)
                ? (int)Math.Round(ptsEl.GetDouble())
                : 0;

            scores.Add(new CriterionScore
            {
                CriterionId   = criterionId,
                CriterionName = criterion.Name,
                Points        = Math.Clamp(points, 0, criterion.MaxPoints),
                MaxPoints     = criterion.MaxPoints
            });
        }
        return scores;
    }
}
