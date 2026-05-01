// == Prompt Lab Service Tests == //
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services.PromptLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class PromptLabServiceTests
{
    private readonly IPromptLabSessionStore _sessionStore = Substitute.For<IPromptLabSessionStore>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<PromptLabService> _logger = Substitute.For<ILogger<PromptLabService>>();
    private readonly PromptLabService _service;

    public PromptLabServiceTests()
    {
        _service = new PromptLabService(_llmService, _sessionStore, _logger);
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

    // == StartChallengeAsync Tests == //

    [Fact]
    public async Task StartChallengeAsync_WithValidId_CreatesAndStoresSession()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        // Generation will fail (no real API key), triggering the fallback to static inputs
        var session = await _service.StartChallengeAsync(challengeId);

        Assert.Equal(challengeId, session.ChallengeId);
        Assert.NotEqual(Guid.Empty, session.SessionId);
        _sessionStore.Received(1).Set(Arg.Is<PromptLabSession>(s => s.ChallengeId == challengeId));
    }

    [Fact]
    public async Task StartChallengeAsync_WithInvalidId_ThrowsChallengeNotFoundException()
    {
        await Assert.ThrowsAsync<ChallengeNotFoundException>(
            () => _service.StartChallengeAsync("does-not-exist"));
    }

    [Fact]
    public async Task StartChallengeAsync_InitializesEmptyAttemptsList()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        var session = await _service.StartChallengeAsync(challengeId);

        Assert.Empty(session.Attempts);
    }

    [Fact]
    public async Task StartChallengeAsync_SessionHasTestInputs()
    {
        var challengeId = _service.GetChallenges()[0].ChallengeId;

        // Generation fails with a test key; fallback ensures static inputs are always present
        var session = await _service.StartChallengeAsync(challengeId);

        Assert.NotEmpty(session.TestInputs);
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
