// == Prompt Lab Service Tests == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services.PromptLab;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class PromptLabServiceTests
{
    private readonly IPromptLabSessionStore _sessionStore = Substitute.For<IPromptLabSessionStore>();
    private readonly ILogger<PromptLabService> _logger = Substitute.For<ILogger<PromptLabService>>();
    private readonly PromptLabService _service;

    public PromptLabServiceTests()
    {
        var options = Options.Create(new AnthropicOptions { ApiKey = "test-key" });
        _service = new PromptLabService(options, _sessionStore, _logger);
    }

    // == Catalog Tests == //

    [Fact]
    public void GetChallenges_ReturnsNonEmptyList()
    {
        var challenges = _service.GetChallenges();

        Assert.NotEmpty(challenges);
    }

    [Fact]
    public void GetChallenge_WithValidId_ReturnsChallenge()
    {
        var firstId = _service.GetChallenges()[0].ChallengeId;

        var challenge = _service.GetChallenge(firstId);

        Assert.Equal(firstId, challenge.ChallengeId);
    }

    [Fact]
    public void GetChallenge_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        Assert.Throws<ChallengeNotFoundException>(
            () => _service.GetChallenge("does-not-exist"));
    }

    // == StartChallenge Tests == //

    [Fact]
    public void StartChallenge_WithValidId_CreatesAndStoresSession()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        var session = _service.StartChallenge(challengeId);

        Assert.Equal(challengeId, session.ChallengeId);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        _sessionStore.Received(1).Set(Arg.Is<PromptLabSession>(s => s.ChallengeId == challengeId));
    }

    [Fact]
    public void StartChallenge_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        Assert.Throws<ChallengeNotFoundException>(
            () => _service.StartChallenge("does-not-exist"));
    }

    [Fact]
    public void StartChallenge_InitializesEmptyAttemptsList()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        var session = _service.StartChallenge(challengeId);

        Assert.Empty(session.Attempts);
    }

    // == SubmitAttemptAsync Boundary Tests == //

    [Fact]
    public async Task SubmitAttemptAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        _sessionStore.Get(Arg.Any<Guid>()).Returns((PromptLabSession?)null);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => _service.SubmitAttemptAsync(Guid.NewGuid(), "be concise", "list planets", CancellationToken.None));
    }
}
