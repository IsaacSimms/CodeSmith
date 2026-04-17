// == Challenge Response DTO == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Models.PromptLab;

namespace CodeSmith.Api.DTOs.PromptLab;

/// <summary>
/// Client-safe representation of a Challenge.
/// Intentionally excludes HiddenAdversarialPrompt and TestInput.ExpectedBehavior
/// to prevent users from gaming the anti-cheat mechanics.
/// </summary>
public class ChallengeResponse
{
    public string ChallengeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string LockedSystemPrompt { get; set; } = string.Empty;
    public List<EditableFieldDto> EditableFields { get; set; } = [];
    public List<TestInputDto> TestInputs { get; set; } = [];           // Label only — no UserMessage, no ExpectedBehavior
    public List<RubricCriterionDto> Rubric { get; set; } = [];

    public static ChallengeResponse FromChallenge(Challenge challenge) => new()
    {
        ChallengeId        = challenge.ChallengeId,
        Title              = challenge.Title,
        Description        = challenge.Description,
        Category           = challenge.Category.ToString(),
        Difficulty         = challenge.Difficulty.ToString(),
        LockedSystemPrompt = challenge.LockedSystemPrompt,
        EditableFields     = challenge.EditableFields.Select(EditableFieldDto.From).ToList(),
        TestInputs         = challenge.TestInputs.Select(TestInputDto.From).ToList(),
        Rubric             = challenge.Rubric.Select(RubricCriterionDto.From).ToList()
    };
}

/// <summary>Client-safe editable field descriptor.</summary>
public class EditableFieldDto
{
    public string FieldType { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;

    public static EditableFieldDto From(EditableField field) => new()
    {
        FieldType    = field.FieldType.ToString(),
        Placeholder  = field.Placeholder,
        DefaultValue = field.DefaultValue
    };
}

/// <summary>
/// Client-safe test input summary — exposes only InputId and Label.
/// UserMessage and ExpectedBehavior are deliberately excluded.
/// </summary>
public class TestInputDto
{
    public string InputId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    public static TestInputDto From(TestInput input) => new()
    {
        InputId = input.InputId,
        Label   = input.Label
    };
}

/// <summary>Client-safe rubric criterion.</summary>
public class RubricCriterionDto
{
    public string CriterionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MaxPoints { get; set; }

    public static RubricCriterionDto From(RubricCriterion criterion) => new()
    {
        CriterionId = criterion.CriterionId,
        Name        = criterion.Name,
        Description = criterion.Description,
        MaxPoints   = criterion.MaxPoints
    };
}
