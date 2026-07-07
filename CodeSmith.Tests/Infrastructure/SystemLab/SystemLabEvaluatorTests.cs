// == System Lab Evaluator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;
using CodeSmith.Infrastructure.Services.SystemLab;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.SystemLab;

public class SystemLabEvaluatorTests
{
    private readonly ILlmServiceFactory _factory    = Substitute.For<ILlmServiceFactory>();
    private readonly ILlmService        _llmService = Substitute.For<ILlmService>();
    private readonly SystemLabEvaluator _evaluator;

    public SystemLabEvaluatorTests()
    {
        _factory.Get(Arg.Any<AiProvider>()).Returns(_llmService);
        _evaluator = new SystemLabEvaluator(_factory);
    }

    // == Happy Path & Totals Math == //

    [Fact]
    public async Task EvaluateAsync_ValidJson_ReturnsScoredAttempt()
    {
        var scenario = MakeScenario(("latency", 5), ("cost", 5));
        SetResponse("""
            {
              "criterionScores": [{ "criterionId": "latency", "points": 4 }, { "criterionId": "cost", "points": 3 }],
              "tradeoffResults": [],
              "dimensionDeductions": [],
              "overallFeedback": "Solid reasoning."
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Equal(7, attempt.RubricScore);
        Assert.Equal(10, attempt.MaxRubricScore);
        Assert.Equal(7, attempt.TotalScore);
        Assert.Equal(10, attempt.MaxScore);
        Assert.Equal("Solid reasoning.", attempt.OverallFeedback);
        Assert.Equal("my justification", attempt.JustificationContent);
    }

    [Fact]
    public async Task EvaluateAsync_DeductionsSubtractFromRubricScore()
    {
        var scenario = MakeScenario(("latency", 10));
        scenario.Dimensions = [new CrossCuttingDimension { Name = "Security", MaxDeduction = 5 }];
        SetResponse("""
            {
              "criterionScores": [{ "criterionId": "latency", "points": 8 }],
              "dimensionDeductions": [{ "dimensionName": "Security", "deduction": 3, "feedback": "Plaintext secrets." }]
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Equal(8, attempt.RubricScore);
        Assert.Equal(5, attempt.TotalScore);          // 8 - 3
        Assert.Equal(10, attempt.MaxScore);           // deductions never lower the max
    }

    [Fact]
    public async Task EvaluateAsync_DeductionsExceedRubricScore_TotalScoreFloorsAtZero()
    {
        var scenario = MakeScenario(("latency", 10));
        scenario.Dimensions = [new CrossCuttingDimension { Name = "Security", MaxDeduction = 10 }];
        SetResponse("""
            {
              "criterionScores": [{ "criterionId": "latency", "points": 2 }],
              "dimensionDeductions": [{ "dimensionName": "Security", "deduction": 9, "feedback": "Bad." }]
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Equal(0, attempt.TotalScore);
    }

    [Fact]
    public async Task EvaluateAsync_MissingArraysAndFeedback_ReturnsEmptyDefaults()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("{}");

        var attempt = await Evaluate(scenario);

        Assert.Empty(attempt.CriterionScores);
        Assert.Empty(attempt.TradeoffResults);
        Assert.Empty(attempt.DimensionDeductions);
        Assert.Equal("", attempt.OverallFeedback);
        Assert.Equal(0, attempt.TotalScore);
    }

    // == JSON Extraction & Failure Mode == //

    [Fact]
    public async Task EvaluateAsync_JsonWrappedInMarkdownFences_ParsesSuccessfully()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("```json\n{ \"criterionScores\": [{ \"criterionId\": \"latency\", \"points\": 5 }] }\n```");

        var attempt = await Evaluate(scenario);

        Assert.Equal(5, attempt.RubricScore);
    }

    [Fact]
    public async Task EvaluateAsync_MalformedJson_ThrowsEvaluationParseException()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("I am sorry, I cannot produce JSON today.");

        await Assert.ThrowsAsync<EvaluationParseException>(() => Evaluate(scenario));
    }

    // == Criterion Score Integrity == //

    [Fact]
    public async Task EvaluateAsync_HallucinatedCriterionId_IsSkipped()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""
            {
              "criterionScores": [
                { "criterionId": "latency", "points": 3 },
                { "criterionId": "made-up", "points": 99 }
              ]
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Single(attempt.CriterionScores);
        Assert.Equal(3, attempt.RubricScore);         // no phantom points
    }

    [Fact]
    public async Task EvaluateAsync_PointsAboveMax_ClampedToCriterionMax()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""{ "criterionScores": [{ "criterionId": "latency", "points": 12 }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Equal(5, attempt.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateAsync_NegativePoints_ClampedToZero()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""{ "criterionScores": [{ "criterionId": "latency", "points": -3 }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Equal(0, attempt.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateAsync_FractionalPoints_RoundedToNearestInt()
    {
        var scenario = MakeScenario(("latency", 10));
        SetResponse("""{ "criterionScores": [{ "criterionId": "latency", "points": 7.7 }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Equal(8, attempt.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateAsync_CriterionEntryMissingPoints_DefaultsToZero()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""{ "criterionScores": [{ "criterionId": "latency" }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Equal(0, attempt.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateAsync_CriterionEntryMissingId_IsSkipped()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""{ "criterionScores": [{ "points": 4 }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Empty(attempt.CriterionScores);
    }

    [Fact]
    public async Task EvaluateAsync_CriterionScore_CarriesNameAndMaxFromRubric()
    {
        var scenario = MakeScenario(("latency", 5));
        SetResponse("""{ "criterionScores": [{ "criterionId": "latency", "points": 4 }] }""");

        var attempt = await Evaluate(scenario);

        Assert.Equal("Criterion latency", attempt.CriterionScores[0].CriterionName);
        Assert.Equal(5, attempt.CriterionScores[0].MaxPoints);
    }

    // == Tradeoff Results == //

    [Fact]
    public async Task EvaluateAsync_TradeoffResults_ParseEngagedAndFeedback()
    {
        var scenario = MakeScenario(("latency", 5));
        scenario.RequiredTradeoffs = ["Why not cache everything?"];
        SetResponse("""
            {
              "tradeoffResults": [{ "tradeoffQuestion": "Why not cache everything?", "engaged": true, "feedback": "Genuine causal reasoning." }]
            }
            """);

        var attempt = await Evaluate(scenario);

        var result = Assert.Single(attempt.TradeoffResults);
        Assert.Equal("Why not cache everything?", result.TradeoffQuestion);
        Assert.True(result.Engaged);
        Assert.Equal("Genuine causal reasoning.", result.Feedback);
    }

    [Fact]
    public async Task EvaluateAsync_TradeoffMissingQuestion_FallsBackToAuthoredQuestionByPosition()
    {
        var scenario = MakeScenario(("latency", 5));
        scenario.RequiredTradeoffs = ["First authored question?", "Second authored question?"];
        SetResponse("""
            {
              "tradeoffResults": [
                { "engaged": true, "feedback": "a" },
                { "engaged": false, "feedback": "b" }
              ]
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Equal("First authored question?", attempt.TradeoffResults[0].TradeoffQuestion);
        Assert.Equal("Second authored question?", attempt.TradeoffResults[1].TradeoffQuestion);
        Assert.False(attempt.TradeoffResults[1].Engaged);
    }

    // == Dimension Deductions == //

    [Fact]
    public async Task EvaluateAsync_DeductionAboveDimensionMax_ClampedToMaxDeduction()
    {
        var scenario = MakeScenario(("latency", 10));
        scenario.Dimensions = [new CrossCuttingDimension { Name = "Security", MaxDeduction = 4 }];
        SetResponse("""
            { "dimensionDeductions": [{ "dimensionName": "Security", "deduction": 25, "feedback": "Bad." }] }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Equal(4, attempt.DimensionDeductions[0].Deduction);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroDeduction_NullsFeedback()
    {
        var scenario = MakeScenario(("latency", 10));
        scenario.Dimensions = [new CrossCuttingDimension { Name = "Security", MaxDeduction = 4 }];
        SetResponse("""
            { "dimensionDeductions": [{ "dimensionName": "Security", "deduction": 0, "feedback": "Irrelevant note." }] }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Null(attempt.DimensionDeductions[0].Feedback);
    }

    [Fact]
    public async Task EvaluateAsync_HallucinatedDimensionName_IsSkipped()
    {
        var scenario = MakeScenario(("latency", 10));
        scenario.Dimensions = [new CrossCuttingDimension { Name = "Security", MaxDeduction = 4 }];
        SetResponse("""
            {
              "criterionScores": [{ "criterionId": "latency", "points": 10 }],
              "dimensionDeductions": [{ "dimensionName": "Made-Up Dimension", "deduction": 50, "feedback": "Invented." }]
            }
            """);

        var attempt = await Evaluate(scenario);

        Assert.Empty(attempt.DimensionDeductions);    // invented dimensions cannot lower the score
        Assert.Equal(10, attempt.TotalScore);
    }

    // == Completion Request Shape == //

    [Fact]
    public async Task EvaluateAsync_SendsAccurateTierWithSystemLabFeature()
    {
        var scenario = MakeScenario(("latency", 5));
        CompletionRequest? captured = null;
        _llmService.CompleteAsync(Arg.Do<CompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "{}" });

        await Evaluate(scenario);

        Assert.NotNull(captured);
        Assert.Equal(ModelTier.Accurate, captured.Tier);
        Assert.Equal("SystemLab:Evaluate", captured.Feature);
        Assert.Equal(1500, captured.MaxTokens);
    }

    [Fact]
    public async Task EvaluateAsync_OpenJudgmentMode_SystemPromptScoresReasoningNotChoice()
    {
        var scenario = MakeScenario(("latency", 5));
        scenario.EvaluationMode = EvaluationMode.OpenJudgment;
        CompletionRequest? captured = null;
        _llmService.CompleteAsync(Arg.Do<CompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "{}" });

        await Evaluate(scenario);

        Assert.NotNull(captured);
        Assert.Contains("Do NOT score based on which design", captured.SystemPrompt);
    }

    [Fact]
    public async Task EvaluateAsync_UserMessageCarriesScenarioAndJustification()
    {
        var scenario = MakeScenario(("latency", 5));
        CompletionRequest? captured = null;
        _llmService.CompleteAsync(Arg.Do<CompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "{}" });

        await Evaluate(scenario);

        Assert.NotNull(captured);
        var userMessage = Assert.Single(captured.Messages).Content;
        Assert.Contains("Test Scenario", userMessage);
        Assert.Contains("my justification", userMessage);
    }

    // == Helpers == //

    private Task<ScenarioAttempt> Evaluate(Scenario scenario) =>
        _evaluator.EvaluateAsync(scenario, "my justification", AiProvider.Anthropic, CancellationToken.None);

    private void SetResponse(string content) =>
        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = content });

    private static Scenario MakeScenario(params (string CriterionId, int MaxPoints)[] rubric) => new()
    {
        ScenarioId     = "test-scenario-01",
        Title          = "Test Scenario",
        Description    = "Design the thing.",
        Constraints    = "Cheaply.",
        Category       = SystemLabCategory.Storage,
        Difficulty     = Difficulty.Easy,
        EvaluationMode = EvaluationMode.TradeoffReasoning,
        Rubric         = [.. rubric.Select(r => new RubricCriterion
        {
            CriterionId = r.CriterionId,
            Name        = $"Criterion {r.CriterionId}",
            MaxPoints   = r.MaxPoints,
            Description = "Test criterion"
        })]
    };
}
