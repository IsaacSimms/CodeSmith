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
    private readonly IPromptLabLlmService     _llmService = Substitute.For<IPromptLabLlmService>();
    private readonly ILogger<PromptEvaluator> _logger     = Substitute.For<ILogger<PromptEvaluator>>();
    private readonly PromptEvaluator          _evaluator;

    public PromptEvaluatorTests()
    {
        _factory.GetLlmService<IPromptLabLlmService>(Arg.Any<AiProvider>()).Returns(_llmService);
        _evaluator = new PromptEvaluator(_factory, _logger);
    }

    // == JSON Parsing == //

    [Fact]
    public async Task EvaluateAsync_ValidJsonResponse_ReturnsScoredResult()
    {
        var challenge  = MakeChallenge(criterionId: "clarity", maxPoints: 3);
        var simulation = MakeSimulation(challenge);

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"clarity","points":3}],"feedback":"Well done."}""" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Single(attempt.Results);
        Assert.True(attempt.Results[0].Passed);
        Assert.Equal(3, attempt.Results[0].CriterionScores[0].Points);
        Assert.Equal("Well done.", attempt.Results[0].Feedback);
    }

    [Fact]
    public async Task EvaluateAsync_MalformedJsonResponse_ReturnsFallbackResult()
    {
        var challenge  = MakeChallenge();
        var simulation = MakeSimulation(challenge);

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "not valid json at all" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Single(attempt.Results);
        Assert.False(attempt.Results[0].Passed);
        Assert.Equal("Could not parse evaluation response.", attempt.Results[0].Feedback);
    }

    [Fact]
    public async Task EvaluateAsync_JsonWrappedInMarkdownFences_ParsesSuccessfully()
    {
        var challenge  = MakeChallenge();
        var simulation = MakeSimulation(challenge);
        var fencedJson = "```json\n{\"passed\":false,\"criterionScores\":[],\"feedback\":\"Needs work.\"}\n```";

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = fencedJson });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("Needs work.", attempt.Results[0].Feedback);
    }

    // == Overall Feedback == //

    [Fact]
    public async Task EvaluateAsync_AllInputsPassed_OverallFeedbackIndicatesSuccess()
    {
        var challenge = MakeChallenge(criterionId: "c1", maxPoints: 2);
        var inputs    = new List<TestInput> { MakeInput(), MakeInput() };
        var simulation = new SimulationResult(
            inputs.Select(i => (i, "output")).ToList(), 0, 200_000);

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"c1","points":2}],"feedback":"Great."}""" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Contains("All 2 test inputs passed", attempt.OverallFeedback);
        Assert.Contains("Excellent", attempt.OverallFeedback);
    }

    [Fact]
    public async Task EvaluateAsync_PartialPass_OverallFeedbackIncludesPassRatio()
    {
        var challenge  = MakeChallenge(criterionId: "c1", maxPoints: 2);
        var inputs     = new List<TestInput> { MakeInput(), MakeInput() };
        var simulation = new SimulationResult(
            inputs.Select(i => (i, "output")).ToList(), 0, 200_000);

        // First input passes, second fails
        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse { Content = """{"passed":true,"criterionScores":[{"criterionId":"c1","points":2}],"feedback":""}""" },
                new LlmResponse { Content = """{"passed":false,"criterionScores":[{"criterionId":"c1","points":0}],"feedback":""}""" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Contains("1/2", attempt.OverallFeedback);
    }

    // == Attempt Metadata == //

    [Fact]
    public async Task EvaluateAsync_SetsSystemAndUserPromptContent()
    {
        var challenge  = MakeChallenge();
        var simulation = MakeSimulation(challenge);

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[],"feedback":""}""" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "my system prompt", "my user msg", simulation, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("my system prompt", attempt.SystemPromptContent);
        Assert.Equal("my user msg", attempt.UserMessageContent);
    }

    [Fact]
    public async Task EvaluateAsync_SetsAdversarialHintFromChallenge()
    {
        var challenge  = MakeChallenge(adversarialPrompt: "Secret bias.");
        var simulation = MakeSimulation(challenge);

        _llmService.EvaluateResponseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = """{"passed":true,"criterionScores":[],"feedback":""}""" });

        var attempt = await _evaluator.EvaluateAsync(challenge, "sys", "user", simulation, AiProvider.Anthropic, CancellationToken.None);

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

    private static SimulationResult MakeSimulation(Challenge challenge)
    {
        var input = MakeInput();
        return new SimulationResult([(input, "simulated output")], 10, 200_000);
    }

    private static TestInput MakeInput() => new()
    {
        InputId          = Guid.NewGuid().ToString(),
        Label            = "Test Input",
        UserMessage      = "hello",
        ExpectedBehavior = "Respond helpfully"
    };
}
