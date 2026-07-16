// == Prompt Evaluator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services.PromptLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class PromptEvaluatorTests
{
    private readonly ILlmServiceFactory       _factory    = Substitute.For<ILlmServiceFactory>();
    private readonly ILlmService             _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<PromptEvaluator> _logger     = Substitute.For<ILogger<PromptEvaluator>>();
    private readonly PromptEvaluator          _evaluator;

    public PromptEvaluatorTests()
    {
        _factory.Get(Arg.Any<AiProvider>()).Returns(_llmService);
        _evaluator = new PromptEvaluator(_factory, _logger);
    }

    private Task<TestInputResult> EvaluateOne(Challenge challenge, TestInput? input = null)
        => _evaluator.EvaluateOneAsync(challenge, input ?? MakeInput(), "simulated output", "user", AiProvider.Anthropic, CancellationToken.None);

    // == JSON Parsing == //

    [Fact]
    public async Task EvaluateOneAsync_ValidJsonResponse_ReturnsScoredResult()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"clarity","points":3}],"feedback":"Well done."}""" });

        var result = await EvaluateOne(challenge);

        Assert.True(result.Passed);
        Assert.Equal(3, result.CriterionScores[0].Points);
        Assert.Equal("Well done.", result.Feedback);
    }

    [Fact]
    public async Task EvaluateOneAsync_MalformedJsonResponse_ReturnsFallbackResult()
    {
        var challenge = MakeChallenge();

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "not valid json at all" });

        var result = await EvaluateOne(challenge);

        Assert.False(result.Passed);
        Assert.Equal("Could not parse evaluation response.", result.Feedback);
    }

    [Fact]
    public async Task EvaluateOneAsync_JsonWrappedInMarkdownFences_ParsesSuccessfully()
    {
        var challenge  = MakeChallenge();
        var fencedJson = "```json\n{\"passed\":false,\"criterionScores\":[],\"feedback\":\"Needs work.\"}\n```";

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = fencedJson });

        var result = await EvaluateOne(challenge);

        Assert.Equal("Needs work.", result.Feedback);
    }

    // == Criterion Score Integrity == //

    [Fact]
    public async Task EvaluateOneAsync_HallucinatedCriterionId_IsSkipped()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"clarity","points":2},{"criterionId":"invented","points":99}],"feedback":""}""" });

        var result = await EvaluateOne(challenge);

        var score = Assert.Single(result.CriterionScores); // no phantom points from invented criteria
        Assert.Equal(2, score.Points);
    }

    [Fact]
    public async Task EvaluateOneAsync_PointsAboveMax_ClampedToCriterionMax()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"clarity","points":12}],"feedback":""}""" });

        var result = await EvaluateOne(challenge);

        Assert.Equal(3, result.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateOneAsync_NegativePoints_ClampedToZero()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":false,"criterionScores":[{"criterionId":"clarity","points":-5}],"feedback":""}""" });

        var result = await EvaluateOne(challenge);

        Assert.Equal(0, result.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateOneAsync_FractionalPoints_RoundedInsteadOfFailingResult()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"clarity","points":2.7}],"feedback":"Close."}""" });

        var result = await EvaluateOne(challenge);

        Assert.True(result.Passed);                   // a fractional score no longer nukes the whole input
        Assert.Equal(3, result.CriterionScores[0].Points);
    }

    [Fact]
    public async Task EvaluateOneAsync_MissingPoints_DefaultsToZeroInsteadOfFailingResult()
    {
        var challenge = MakeChallenge(criterionId: "clarity", maxPoints: 3);

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":false,"criterionScores":[{"criterionId":"clarity"}],"feedback":"Weak."}""" });

        var result = await EvaluateOne(challenge);

        Assert.Equal(0, result.CriterionScores[0].Points);
        Assert.Equal("Weak.", result.Feedback);
    }

    // == Attempt Assembly == //

    [Fact]
    public void AssembleAttempt_AllInputsPassed_OverallFeedbackIndicatesSuccess()
    {
        var challenge = MakeChallenge(criterionId: "c1", maxPoints: 2);
        var results   = new List<TestInputResult> { PassedResult(2), PassedResult(2) };

        var attempt = _evaluator.AssembleAttempt(challenge, "sys", "user", results);

        Assert.Contains("All 2 test inputs passed", attempt.OverallFeedback);
        Assert.Contains("Excellent", attempt.OverallFeedback);
        Assert.Equal(4, attempt.TotalScore);
        Assert.Equal(4, attempt.MaxScore);   // 2 inputs × 2 max points
    }

    [Fact]
    public void AssembleAttempt_PartialPass_OverallFeedbackIncludesPassRatio()
    {
        var challenge = MakeChallenge(criterionId: "c1", maxPoints: 2);
        var results   = new List<TestInputResult> { PassedResult(2), FailedResult(0) };

        var attempt = _evaluator.AssembleAttempt(challenge, "sys", "user", results);

        Assert.Contains("1/2", attempt.OverallFeedback);
    }

    [Fact]
    public void AssembleAttempt_SetsSystemAndUserPromptContent()
    {
        var attempt = _evaluator.AssembleAttempt(MakeChallenge(), "my system prompt", "my user msg", [PassedResult(2)]);

        Assert.Equal("my system prompt", attempt.SystemPromptContent);
        Assert.Equal("my user msg", attempt.UserMessageContent);
    }

    [Fact]
    public void AssembleAttempt_SetsAdversarialHintFromChallenge()
    {
        var challenge = MakeChallenge(adversarialPrompt: "Secret bias.");

        var attempt = _evaluator.AssembleAttempt(challenge, "sys", "user", [PassedResult(2)]);

        Assert.Equal("Secret bias.", attempt.AdversarialHint);
    }

    // == Helpers == //

    private static Challenge MakeChallenge(string criterionId = "c1", int maxPoints = 2, string adversarialPrompt = "")
    {
        return new Challenge
        {
            ChallengeId             = "test-01",
            Title                   = "Test Challenge",
            Description             = "Do the thing.",
            LockedSystemPrompt      = "You are an assistant.",
            HiddenAdversarialPrompt = adversarialPrompt,
            EditableFields          = [],
            TestInputs              = [],
            Rubric                  =
            [
                new RubricCriterion { CriterionId = criterionId, Name = "Test Criterion", MaxPoints = maxPoints, Description = "Good" }
            ]
        };
    }

    private static TestInput MakeInput() => new()
    {
        InputId          = Guid.NewGuid().ToString(),
        Label            = "Test Input",
        UserMessage      = "hello",
        ExpectedBehavior = "Respond helpfully"
    };

    private static TestInputResult PassedResult(int points) => new()
    {
        InputId         = Guid.NewGuid().ToString(),
        Label           = "Test Input",
        Passed          = true,
        CriterionScores = [new CriterionScore { CriterionId = "c1", CriterionName = "Test Criterion", Points = points, MaxPoints = 2 }]
    };

    private static TestInputResult FailedResult(int points) => new()
    {
        InputId         = Guid.NewGuid().ToString(),
        Label           = "Test Input",
        Passed          = false,
        CriterionScores = [new CriterionScore { CriterionId = "c1", CriterionName = "Test Criterion", Points = points, MaxPoints = 2 }]
    };
}
