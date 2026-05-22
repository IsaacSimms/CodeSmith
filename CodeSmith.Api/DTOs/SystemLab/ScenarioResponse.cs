// == Scenario Response DTO == //
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;

namespace CodeSmith.Api.DTOs.SystemLab;

/// <summary>
/// Client-safe representation of a Scenario.
/// SecurityPitfalls are intentionally excluded to prevent gaming the evaluator.
/// </summary>
public class ScenarioResponse
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string EvaluationMode { get; set; } = string.Empty;
    public List<ScenarioRubricCriterionDto> Rubric { get; set; } = [];
    public List<string> RequiredTradeoffs { get; set; } = [];  // Visible to user; authored as causal questions

    public static ScenarioResponse FromScenario(Scenario scenario) => new()
    {
        ScenarioId     = scenario.ScenarioId,
        Title          = scenario.Title,
        Description    = scenario.Description,
        Constraints    = scenario.Constraints,
        Category       = scenario.Category.ToString(),
        Difficulty     = scenario.Difficulty.ToString(),
        EvaluationMode = scenario.EvaluationMode.ToString(),
        Rubric         = scenario.Rubric.Select(ScenarioRubricCriterionDto.From).ToList(),
        RequiredTradeoffs = scenario.RequiredTradeoffs.ToList()
    };
}

/// <summary>Client-safe rubric criterion for a scenario.</summary>
public class ScenarioRubricCriterionDto
{
    public string CriterionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxPoints { get; set; }

    public static ScenarioRubricCriterionDto From(RubricCriterion criterion) => new()
    {
        CriterionId = criterion.CriterionId,
        Name        = criterion.Name,
        Description = criterion.Description,
        MaxPoints   = criterion.MaxPoints
    };
}
