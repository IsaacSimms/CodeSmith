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
    // == Helpers == //

    private static TutoringService BuildService(
        IProblemGenerator?             problemGenerator = null,
        IGuidanceConversation?         guidance         = null,
        ISessionStore<ProblemSession>? store            = null,
        ICodeExecutionService?         codeExec         = null,
        ITutoringPromptTemplates?      templates        = null,
        ILogger<TutoringService>?      logger           = null)
        => new(
            problemGenerator ?? Substitute.For<IProblemGenerator>(),
            guidance         ?? Substitute.For<IGuidanceConversation>(),
            store            ?? Substitute.For<ISessionStore<ProblemSession>>(),
            codeExec         ?? Substitute.For<ICodeExecutionService>(),
            templates        ?? Substitute.For<ITutoringPromptTemplates>(),
            logger           ?? Substitute.For<ILogger<TutoringService>>());

    // Builds a guidance substitute that returns the given reply, so the orchestrator can project it.
    private static IGuidanceConversation GuidanceReturning(LlmResponse response)
    {
        var guidance = Substitute.For<IGuidanceConversation>();
        guidance.RunTurnAsync(Arg.Any<AiProvider>(), Arg.Any<List<ChatMessage>>(), Arg.Any<GuidanceTurnRequest>(), Arg.Any<Action>(), Arg.Any<CancellationToken>())
            .Returns(response);
        return guidance;
    }

    private static ITutoringPromptTemplates TemplatesReturning(string systemPrompt)
    {
        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<GuidanceMode>())
            .Returns(systemPrompt);
        return templates;
    }

    // == Session Not Found Tests == //

    [Fact]
    public async Task GetGuidanceAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var service = BuildService(store: sessionStore);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", null, GuidanceMode.Guidance, CancellationToken.None));
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ThrowsSessionNotFoundButAcceptsParam()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var service = BuildService(store: sessionStore);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", "int x = 42;", GuidanceMode.Guidance, CancellationToken.None));
    }

    // == Problem Generation Happy Path == //

    [Fact]
    public async Task GenerateProblemAsync_ReturnsSessionWithCorrectFields()
    {
        var problemGenerator = Substitute.For<IProblemGenerator>();
        problemGenerator.GenerateAsync(Difficulty.Hard, Language.Python, AiProvider.Anthropic, Arg.Any<CancellationToken>())
            .Returns(("Find the nth Fibonacci number.", "def fib(n): pass"));

        var service = BuildService(problemGenerator: problemGenerator);

        var session = await service.GenerateProblemAsync(Difficulty.Hard, Language.Python, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal(Difficulty.Hard,                  session.Difficulty);
        Assert.Equal(Language.Python,                  session.Language);
        Assert.Equal(AiProvider.Anthropic,             session.Provider);
        Assert.Equal("Find the nth Fibonacci number.", session.ProblemDescription);
        Assert.Equal("def fib(n): pass",               session.StarterCode);
    }

    [Fact]
    public async Task GenerateProblemAsync_StoresNewSession()
    {
        var problemGenerator = Substitute.For<IProblemGenerator>();
        problemGenerator.GenerateAsync(Arg.Any<Difficulty>(), Arg.Any<Language>(), Arg.Any<AiProvider>(), Arg.Any<CancellationToken>())
            .Returns(("Reverse a string.", "string Reverse(string s) => null!;"));

        var store = Substitute.For<ISessionStore<ProblemSession>>();

        var service = BuildService(problemGenerator: problemGenerator, store: store);

        var session = await service.GenerateProblemAsync(Difficulty.Easy, Language.CSharp, AiProvider.Anthropic, CancellationToken.None);

        store.Received(1).Set(Arg.Is<ProblemSession>(s => s.SessionId == session.SessionId));
    }

    // == Guidance Happy Path == //
    // The turn mechanics (append user/assistant, history window, rollback, error-wrapping) live in
    // GuidanceConversation — see GuidanceConversationTests. These cover the orchestrator's own job:
    // building the prompt from templates, delegating with the Tutoring:Guidance feature, and projecting
    // the returned completion into a ChatResponse.

    [Fact]
    public async Task GetGuidanceAsync_DelegatesWithSessionHistoryAndProjectsResult()
    {
        var session = new ProblemSession
        {
            Language           = Language.Go,
            Provider           = AiProvider.Anthropic,
            ProblemDescription = "Write a concurrent fan-out.",
            StarterCode        = "func fanOut() {}"
        };

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        var guidance = GuidanceReturning(new LlmResponse { Content = "Think about goroutines.", InputTokensUsed = 75, ContextWindowSize = 200_000 });

        var service = BuildService(guidance: guidance, store: store, templates: TemplatesReturning("system prompt"));

        var response = await service.GetGuidanceAsync(session.SessionId, "I'm stuck", null, GuidanceMode.Guidance, CancellationToken.None);

        // Projects the completion's token info into the ChatResponse
        Assert.Equal("Think about goroutines.", response.Response);
        Assert.Equal(75,      response.ContextTokensUsed);
        Assert.Equal(200_000, response.ContextWindowSize);

        // Delegates the turn against the session's own history, provider, and the Tutoring:Guidance feature
        await guidance.Received(1).RunTurnAsync(
            session.Provider,
            session.Messages,
            Arg.Is<GuidanceTurnRequest>(r => r.Feature == "Tutoring:Guidance" && r.UserMessage == "I'm stuck"),
            Arg.Any<Action>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ForwardsToTemplates()
    {
        var session = new ProblemSession
        {
            Language           = Language.TypeScript,
            Provider           = AiProvider.Anthropic,
            ProblemDescription = "Problem.",
            StarterCode        = "const solve = () => {};"
        };

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        var templates = TemplatesReturning("system prompt");
        var service   = BuildService(guidance: GuidanceReturning(new LlmResponse { Content = "Good start." }), store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "review my code", "const solve = () => 42;", GuidanceMode.Guidance, CancellationToken.None);

        templates.Received(1).GuidanceSystemPrompt(
            Language.TypeScript,
            Arg.Any<string>(),
            Arg.Any<string>(),
            "const solve = () => 42;",
            GuidanceMode.Guidance);
    }

    [Fact]
    public async Task GetGuidanceAsync_WithCodeAnalysisFlag_ForwardsFlagToTemplates()
    {
        var session = new ProblemSession
        {
            Language           = Language.Java,
            Provider           = AiProvider.Anthropic,
            ProblemDescription = "Problem.",
            StarterCode        = "void solve() {}"
        };

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        var templates = TemplatesReturning("code analysis system prompt");
        var service   = BuildService(guidance: GuidanceReturning(new LlmResponse { Content = "Your output looks correct." }), store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "I ran my code", null, guidanceMode: GuidanceMode.CodeAnalysis, CancellationToken.None);

        templates.Received(1).GuidanceSystemPrompt(
            Language.Java,
            Arg.Any<string>(),
            Arg.Any<string>(),
            null,
            GuidanceMode.CodeAnalysis);
    }

    // == Code Execution Happy Path == //

    [Fact]
    public async Task RunCodeAsync_ReturnsExecutionResult()
    {
        var session = new ProblemSession { Language = Language.Python, Provider = AiProvider.Anthropic };

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        var expectedResult = new CodeExecutionResult { Stdout = "42\n", ExitCode = 0 };
        var codeExec = Substitute.For<ICodeExecutionService>();
        codeExec.ExecuteAsync(Language.Python, "print(42)", Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var service = BuildService(store: store, codeExec: codeExec);

        var result = await service.RunCodeAsync(session.SessionId, Language.Python, "print(42)", CancellationToken.None);

        Assert.Equal("42\n", result.Stdout);
        Assert.Equal(0,      result.ExitCode);
    }

    [Fact]
    public async Task RunCodeAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var service = BuildService(store: store);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.RunCodeAsync(Guid.NewGuid(), Language.Python, "print(42)", CancellationToken.None));
    }

}
