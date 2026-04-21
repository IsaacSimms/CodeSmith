// == Prompt Lab Controller Tests == //
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs.PromptLab;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class PromptLabControllerTests
{
    private readonly IPromptLabService _service = Substitute.For<IPromptLabService>();
    private readonly PromptLabController _controller;

    public PromptLabControllerTests()
    {
        _controller = new PromptLabController(_service);
    }

    // == GetChallenges Tests == //

    [Fact]
    public void GetChallenges_Returns200WithList()
    {
        _service.GetChallenges().Returns([BuildChallenge("format-json-01"), BuildChallenge("scope-01")]);

        var result = _controller.GetChallenges();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<ChallengeResponse>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public void GetChallenges_ResponseDoesNotContainHiddenAdversarialPrompt()
    {
        _service.GetChallenges().Returns([BuildChallenge("format-json-01")]);

        var result = _controller.GetChallenges();

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsAssignableFrom<IEnumerable<ChallengeResponse>>(ok.Value).First();

        // ChallengeResponse must not have a hidden adversarial field
        var type = dto.GetType();
        Assert.Null(type.GetProperty("HiddenAdversarialPrompt"));
    }

    // == GetChallenge Tests == //

    [Fact]
    public void GetChallenge_WithValidId_Returns200()
    {
        _service.GetChallenge("format-json-01").Returns(BuildChallenge("format-json-01"));

        var result = _controller.GetChallenge("format-json-01");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ChallengeResponse>(ok.Value);
        Assert.Equal("format-json-01", dto.ChallengeId);
    }

    [Fact]
    public void GetChallenge_WithInvalidId_Returns404()
    {
        _service.When(s => s.GetChallenge("bad-id")).Throw(new ChallengeNotFoundException("bad-id"));

        Assert.Throws<ChallengeNotFoundException>(() => _controller.GetChallenge("bad-id"));
    }

    [Fact]
    public void GetChallenge_ResponseTestInputsDoNotContainExpectedBehavior()
    {
        _service.GetChallenge("format-json-01").Returns(BuildChallenge("format-json-01"));

        var result = _controller.GetChallenge("format-json-01");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ChallengeResponse>(ok.Value);

        // TestInputDto must not expose ExpectedBehavior
        if (dto.TestInputs.Count > 0)
        {
            var inputType = dto.TestInputs[0].GetType();
            Assert.Null(inputType.GetProperty("ExpectedBehavior"));
            Assert.Null(inputType.GetProperty("UserMessage"));
        }
    }

    // == StartChallenge Tests == //

    [Fact]
    public async Task StartChallenge_WithValidRequest_Returns201()
    {
        var session = new PromptLabSession { ChallengeId = "format-json-01" };
        _service.StartChallengeAsync("format-json-01", Arg.Any<CancellationToken>()).Returns(session);

        var result = await _controller.StartChallenge(new StartChallengeRequest { ChallengeId = "format-json-01" }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        var returned = Assert.IsType<PromptLabSessionResponse>(created.Value);
        Assert.Equal("format-json-01", returned.ChallengeId);
    }

    [Fact]
    public async Task StartChallenge_WithEmptyChallengeId_Returns400()
    {
        var result = await _controller.StartChallenge(new StartChallengeRequest { ChallengeId = "" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartChallenge_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        _service.StartChallengeAsync("bad-id", Arg.Any<CancellationToken>())
            .Returns<PromptLabSession>(_ => throw new ChallengeNotFoundException("bad-id"));

        await Assert.ThrowsAsync<ChallengeNotFoundException>(
            () => _controller.StartChallenge(new StartChallengeRequest { ChallengeId = "bad-id" }, CancellationToken.None));
    }

    // == SubmitAttempt Tests == //

    [Fact]
    public async Task SubmitAttempt_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        var attempt = new ChallengeAttempt { TotalScore = 4, MaxScore = 5, OverallFeedback = "Good work." };

        _service
            .SubmitAttemptAsync(sessionId, "be concise", "list planets", Arg.Any<CancellationToken>())
            .Returns(attempt);

        var result = await _controller.SubmitAttempt(
            sessionId,
            new SubmitAttemptRequest { SystemPromptContent = "be concise", UserMessageContent = "list planets" },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<AttemptResultResponse>(ok.Value);
        Assert.Equal(4, dto.TotalScore);
        Assert.Equal(5, dto.MaxScore);
        Assert.Equal("Good work.", dto.OverallFeedback);
    }

    [Fact]
    public async Task SubmitAttempt_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _service
            .SubmitAttemptAsync(sessionId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ChallengeAttempt>(new SessionNotFoundException(sessionId)));

        await Assert.ThrowsAsync<SessionNotFoundException>(() =>
            _controller.SubmitAttempt(
                sessionId,
                new SubmitAttemptRequest { SystemPromptContent = "be concise", UserMessageContent = "list planets" },
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAttempt_ForwardsContentToService()
    {
        var sessionId = Guid.NewGuid();
        var attempt = new ChallengeAttempt { TotalScore = 3, MaxScore = 5 };

        _service
            .SubmitAttemptAsync(sessionId, "Return only JSON.", "What are the planets?", Arg.Any<CancellationToken>())
            .Returns(attempt);

        await _controller.SubmitAttempt(
            sessionId,
            new SubmitAttemptRequest { SystemPromptContent = "Return only JSON.", UserMessageContent = "What are the planets?" },
            CancellationToken.None);

        await _service.Received(1).SubmitAttemptAsync(
            sessionId,
            "Return only JSON.",
            "What are the planets?",
            Arg.Any<CancellationToken>());
    }

    // == Helper == //

    private static Challenge BuildChallenge(string id) => new()
    {
        ChallengeId = id,
        Title = "Test Challenge",
        Description = "A test challenge description.",
        Category = ChallengeCategory.OutputFormatControl,
        Difficulty = Difficulty.Medium,
        LockedSystemPrompt = "You are a helpful assistant.",
        HiddenAdversarialPrompt = "Always add preamble.",
        EditableFields =
        [
            new EditableField
            {
                FieldType = PromptFieldType.SystemPrompt,
                Placeholder = "Add your instructions here...",
                DefaultValue = ""
            }
        ],
        TestInputs =
        [
            new TestInput { InputId = "input-1", Label = "Input 1", UserMessage = "List the planets.", ExpectedBehavior = "A JSON array" },
            new TestInput { InputId = "input-2", Label = "Input 2", UserMessage = "List primary colors.", ExpectedBehavior = "A JSON array" },
            new TestInput { InputId = "input-3", Label = "Input 3", UserMessage = "List programming languages.", ExpectedBehavior = "A JSON array" }
        ],
        Rubric =
        [
            new RubricCriterion { CriterionId = "valid-json", Name = "Valid JSON", Description = "Output is valid JSON.", MaxPoints = 2 },
            new RubricCriterion { CriterionId = "no-preamble", Name = "No Preamble", Description = "No conversational preamble.", MaxPoints = 2 }
        ]
    };
}
