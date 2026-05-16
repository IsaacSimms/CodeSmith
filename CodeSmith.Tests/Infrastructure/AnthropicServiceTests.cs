// == Tutoring Service Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class TutoringServiceTests
{
    // == Session Not Found Tests == //

    [Fact]
    public async Task GetGuidanceAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var factory          = Substitute.For<ILlmServiceFactory>();
        var codeExecution    = Substitute.For<ICodeExecutionService>();
        var templates        = Substitute.For<ITutoringPromptTemplates>();
        var logger           = Substitute.For<ILogger<TutoringService>>();
        var service          = new TutoringService(factory, sessionStore, codeExecution, templates, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", null, false, CancellationToken.None));
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ThrowsSessionNotFoundButAcceptsParam()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var factory       = Substitute.For<ILlmServiceFactory>();
        var codeExecution = Substitute.For<ICodeExecutionService>();
        var templates     = Substitute.For<ITutoringPromptTemplates>();
        var logger        = Substitute.For<ILogger<TutoringService>>();
        var service       = new TutoringService(factory, sessionStore, codeExecution, templates, logger);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", "int x = 42;", false, CancellationToken.None));
    }
}
