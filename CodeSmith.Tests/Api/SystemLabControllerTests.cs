// == System Lab Controller Tests == //
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs.SystemLab;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Core.Models.SystemLab;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CodeSmith.Tests.Api;

public class SystemLabControllerTests
{
    private readonly ISystemLabService  _service    = Substitute.For<ISystemLabService>();
    private readonly SystemLabController _controller;

    public SystemLabControllerTests()
    {
        _controller = new SystemLabController(_service);
    }

    // == GetScenarios Tests == //

    [Fact]
    public void GetScenarios_Returns200WithList()
    {
        _service.GetScenarios().Returns([BuildScenario("identity-rbac-easy-01"), BuildScenario("compute-serverless-med-01")]);

        var result = _controller.GetScenarios();

        var ok   = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<ScenarioResponse>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public void GetScenarios_ResponseDoesNotContainDimensions()
    {
        _service.GetScenarios().Returns([BuildScenario("identity-storage-access-easy-01")]);

        var result = _controller.GetScenarios();

        var ok  = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsAssignableFrom<IEnumerable<ScenarioResponse>>(ok.Value).First();
        // Dimensions (and their pitfall lists) must never be exposed to the client
        Assert.Null(dto.GetType().GetProperty("Dimensions"));
    }

    // == GetScenario Tests == //

    [Fact]
    public void GetScenario_WithValidId_Returns200()
    {
        _service.GetScenario("identity-rbac-easy-01").Returns(BuildScenario("identity-rbac-easy-01"));

        var result = _controller.GetScenario("identity-rbac-easy-01");

        var ok  = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ScenarioResponse>(ok.Value);
        Assert.Equal("identity-rbac-easy-01", dto.ScenarioId);
    }

    [Fact]
    public void GetScenario_WithInvalidId_ThrowsScenarioNotFoundException()
    {
        _service.When(s => s.GetScenario("bad-id")).Throw(new ScenarioNotFoundException("bad-id"));

        Assert.Throws<ScenarioNotFoundException>(() => _controller.GetScenario("bad-id"));
    }

    // == StartSession Tests == //

    [Fact]
    public async Task StartSession_WithValidRequest_Returns201()
    {
        var session = new SystemLabSession { ScenarioId = "identity-rbac-easy-01" };
        _service.StartSessionAsync("identity-rbac-easy-01", Arg.Any<AiProvider>(), Arg.Any<CancellationToken>()).Returns(session);

        var result = await _controller.StartSession(new StartSystemLabSessionRequest { ScenarioId = "identity-rbac-easy-01" }, CancellationToken.None);

        var created  = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        var returned = Assert.IsType<SystemLabSessionResponse>(created.Value);
        Assert.Equal("identity-rbac-easy-01", returned.ScenarioId);
    }

    [Fact]
    public async Task StartSession_WithEmptyScenarioId_Returns400()
    {
        var result = await _controller.StartSession(new StartSystemLabSessionRequest { ScenarioId = "" }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartSession_WithInvalidId_ThrowsScenarioNotFoundException()
    {
        _service.StartSessionAsync("bad-id", Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ScenarioNotFoundException("bad-id"));

        await Assert.ThrowsAsync<ScenarioNotFoundException>(
            () => _controller.StartSession(new StartSystemLabSessionRequest { ScenarioId = "bad-id" }, CancellationToken.None));
    }

    // == SubmitAttempt Tests == //

    [Fact]
    public async Task SubmitAttempt_WithValidRequest_Returns200()
    {
        var sessionId = Guid.NewGuid();
        var attempt   = new ScenarioAttempt
        {
            TotalScore      = 8,
            MaxScore        = 10,
            RubricScore     = 9,
            MaxRubricScore  = 10,
            DimensionDeductions =
            [
                new DimensionDeduction { DimensionName = "Security", Deduction = 1, Feedback = "Insecure pattern detected." }
            ],
            OverallFeedback = "Strong tradeoff reasoning."
        };

        _service.SubmitAttemptAsync(sessionId, "my justification", Arg.Any<CancellationToken>())
            .Returns(attempt);

        var result = await _controller.SubmitAttempt(
            sessionId,
            new SubmitJustificationRequest { JustificationContent = "my justification" },
            CancellationToken.None);

        var ok  = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SystemLabAttemptResultResponse>(ok.Value);
        Assert.Equal(8,  dto.TotalScore);
        Assert.Equal(10, dto.MaxScore);
        Assert.Single(dto.DimensionDeductions);
        Assert.Equal(1,          dto.DimensionDeductions[0].Deduction);
        Assert.Equal("Security", dto.DimensionDeductions[0].DimensionName);
        Assert.Equal("Strong tradeoff reasoning.", dto.OverallFeedback);
    }

    [Fact]
    public async Task SubmitAttempt_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _service.SubmitAttemptAsync(sessionId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ScenarioAttempt>(new SessionNotFoundException(sessionId)));

        await Assert.ThrowsAsync<SessionNotFoundException>(() =>
            _controller.SubmitAttempt(
                sessionId,
                new SubmitJustificationRequest { JustificationContent = "my justification" },
                CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAttempt_WithEmptyJustification_Returns400()
    {
        var result = await _controller.SubmitAttempt(
            Guid.NewGuid(),
            new SubmitJustificationRequest { JustificationContent = "" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // == Chat Tests == //

    [Fact]
    public async Task Chat_WithValidRequest_Returns200WithResponse()
    {
        var sessionId = Guid.NewGuid();
        _service.ChatAsync(sessionId, "what should I consider?", null, Arg.Any<CancellationToken>())
            .Returns("Think about the RTO constraint first.");

        var result = await _controller.Chat(
            sessionId,
            new SystemLabChatRequest { Message = "what should I consider?" },
            CancellationToken.None);

        var ok  = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<SystemLabChatResponse>(ok.Value);
        Assert.Equal("Think about the RTO constraint first.", dto.Response);
    }

    [Fact]
    public async Task Chat_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionId = Guid.NewGuid();
        _service.ChatAsync(sessionId, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new SessionNotFoundException(sessionId)));

        await Assert.ThrowsAsync<SessionNotFoundException>(() =>
            _controller.Chat(
                sessionId,
                new SystemLabChatRequest { Message = "help" },
                CancellationToken.None));
    }

    [Fact]
    public async Task Chat_WithEmptyMessage_Returns400()
    {
        var result = await _controller.Chat(
            Guid.NewGuid(),
            new SystemLabChatRequest { Message = "" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // == Helper == //

    private static Scenario BuildScenario(string id) => new()
    {
        ScenarioId      = id,
        Title           = "Test Scenario",
        Description     = "A test scenario description.",
        Constraints     = "Must use zone-redundant storage.",
        Category        = SystemLabCategory.IdentityAndGovernance,
        Difficulty      = Difficulty.Easy,
        EvaluationMode  = EvaluationMode.SingleAnswer,
        Rubric          =
        [
            new RubricCriterion { CriterionId = "rbac-least-priv", Name = "Least Privilege", Description = "Uses minimum required permissions.", MaxPoints = 3 },
            new RubricCriterion { CriterionId = "rbac-audit",      Name = "Auditability",   Description = "Design supports audit logging.",   MaxPoints = 2 }
        ],
        RequiredTradeoffs =
        [
            "Why is a custom role preferred over a built-in Owner role for this workload?"
        ],
        Dimensions =
        [
            new CrossCuttingDimension
            {
                Name         = "Security",
                Pitfalls     = ["Granting subscription-level Owner to the application identity"],
                MaxDeduction = 3
            }
        ]
    };
}
