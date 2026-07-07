// == LlmJson Tests == //
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services;

namespace CodeSmith.Tests.Infrastructure;

public class LlmJsonTests
{
    // == ExtractJson == //

    [Fact]
    public void ExtractJson_PlainJson_ReturnsTrimmedInput()
    {
        Assert.Equal("""{"a":1}""", LlmJson.ExtractJson("  {\"a\":1}  "));
    }

    [Fact]
    public void ExtractJson_FencedWithLanguageTag_StripsFences()
    {
        Assert.Equal("""{"a":1}""", LlmJson.ExtractJson("```json\n{\"a\":1}\n```"));
    }

    [Fact]
    public void ExtractJson_FencedWithoutLanguageTag_StripsFences()
    {
        Assert.Equal("""{"a":1}""", LlmJson.ExtractJson("```\n{\"a\":1}\n```"));
    }

    [Fact]
    public void ExtractJson_UnclosedFence_ReturnsTrimmedInputUnchanged()
    {
        Assert.Equal("```json\n{\"a\":1}", LlmJson.ExtractJson("```json\n{\"a\":1}"));
    }

    // == Parse == //

    [Fact]
    public void Parse_ValidJson_ReturnsDocument()
    {
        using var doc = LlmJson.Parse("""{"key":"value"}""");

        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_FencedJson_ReturnsDocument()
    {
        using var doc = LlmJson.Parse("```json\n{\"key\":\"value\"}\n```");

        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsEvaluationParseException()
    {
        var ex = Assert.Throws<EvaluationParseException>(() => LlmJson.Parse("not json"));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    // == Deserialize == //

    [Fact]
    public void Deserialize_ValidArray_ReturnsTypedList()
    {
        var items = LlmJson.Deserialize<List<TestItemDto>>("""[{"name":"a"},{"name":"b"}]""");

        Assert.Equal(2, items.Count);
        Assert.Equal("a", items[0].Name);
    }

    [Fact]
    public void Deserialize_FencedArray_ReturnsTypedList()
    {
        var items = LlmJson.Deserialize<List<TestItemDto>>("```json\n[{\"name\":\"a\"}]\n```");

        Assert.Single(items);
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsEvaluationParseException()
    {
        Assert.Throws<EvaluationParseException>(() => LlmJson.Deserialize<List<TestItemDto>>("nope"));
    }

    [Fact]
    public void Deserialize_JsonNullLiteral_ThrowsEvaluationParseException()
    {
        Assert.Throws<EvaluationParseException>(() => LlmJson.Deserialize<List<TestItemDto>>("null"));
    }

    // == ParseCriterionScores == //

    [Fact]
    public void ParseCriterionScores_ValidEntries_MapNamesAndMaxFromRubric()
    {
        var scores = ParseScores("""{"criterionScores":[{"criterionId":"c1","points":3}]}""", ("c1", 5));

        var score = Assert.Single(scores);
        Assert.Equal("c1", score.CriterionId);
        Assert.Equal("Criterion c1", score.CriterionName);
        Assert.Equal(3, score.Points);
        Assert.Equal(5, score.MaxPoints);
    }

    [Fact]
    public void ParseCriterionScores_HallucinatedId_IsSkipped()
    {
        var scores = ParseScores(
            """{"criterionScores":[{"criterionId":"c1","points":2},{"criterionId":"fake","points":99}]}""", ("c1", 5));

        Assert.Single(scores);
        Assert.Equal("c1", scores[0].CriterionId);
    }

    [Fact]
    public void ParseCriterionScores_MissingId_IsSkipped()
    {
        var scores = ParseScores("""{"criterionScores":[{"points":4}]}""", ("c1", 5));

        Assert.Empty(scores);
    }

    [Fact]
    public void ParseCriterionScores_PointsAboveMax_ClampedToMax()
    {
        var scores = ParseScores("""{"criterionScores":[{"criterionId":"c1","points":12}]}""", ("c1", 5));

        Assert.Equal(5, scores[0].Points);
    }

    [Fact]
    public void ParseCriterionScores_NegativePoints_ClampedToZero()
    {
        var scores = ParseScores("""{"criterionScores":[{"criterionId":"c1","points":-4}]}""", ("c1", 5));

        Assert.Equal(0, scores[0].Points);
    }

    [Fact]
    public void ParseCriterionScores_FractionalPoints_RoundedToNearestInt()
    {
        var scores = ParseScores("""{"criterionScores":[{"criterionId":"c1","points":7.7}]}""", ("c1", 10));

        Assert.Equal(8, scores[0].Points);
    }

    [Fact]
    public void ParseCriterionScores_MissingPoints_DefaultsToZero()
    {
        var scores = ParseScores("""{"criterionScores":[{"criterionId":"c1"}]}""", ("c1", 5));

        Assert.Equal(0, scores[0].Points);
    }

    [Fact]
    public void ParseCriterionScores_MissingArray_ReturnsEmptyList()
    {
        var scores = ParseScores("{}", ("c1", 5));

        Assert.Empty(scores);
    }

    // == Helpers == //

    private static List<CriterionScore> ParseScores(string json, params (string Id, int Max)[] rubric)
    {
        using var doc = LlmJson.Parse(json);
        var criteria = rubric.Select(r => new RubricCriterion
        {
            CriterionId = r.Id,
            Name        = $"Criterion {r.Id}",
            MaxPoints   = r.Max,
            Description = "Test criterion"
        }).ToList();

        return LlmJson.ParseCriterionScores(criteria, doc.RootElement);
    }

    private sealed class TestItemDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
