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
        ILlmServiceFactory?            factory          = null,
        ISessionStore<ProblemSession>? store            = null,
        ICodeExecutionService?         codeExec         = null,
        ITutoringPromptTemplates?      templates        = null,
        ILogger<TutoringService>?      logger           = null)
        => new(
            problemGenerator ?? Substitute.For<IProblemGenerator>(),
            factory          ?? Substitute.For<ILlmServiceFactory>(),
            store            ?? Substitute.For<ISessionStore<ProblemSession>>(),
            codeExec         ?? Substitute.For<ICodeExecutionService>(),
            templates        ?? Substitute.For<ITutoringPromptTemplates>(),
            logger           ?? Substitute.For<ILogger<TutoringService>>());

    // == Session Not Found Tests == //

    [Fact]
    public async Task GetGuidanceAsync_WithUnknownSession_ThrowsSessionNotFoundException()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var service = BuildService(store: sessionStore);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", null, false, CancellationToken.None));
    }

    [Fact]
    public async Task GetGuidanceAsync_WithEditorContent_ThrowsSessionNotFoundButAcceptsParam()
    {
        var sessionStore = Substitute.For<ISessionStore<ProblemSession>>();
        sessionStore.Get(Arg.Any<string>()).Returns((ProblemSession?)null);

        var service = BuildService(store: sessionStore);

        await Assert.ThrowsAsync<SessionNotFoundException>(
            () => service.GetGuidanceAsync(Guid.NewGuid(), "help me", "int x = 42;", false, CancellationToken.None));
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

    [Fact]
    public async Task GetGuidanceAsync_ReturnsLlmResponseWithTokenInfo()
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

        var llmService = Substitute.For<ITutoringLlmService>();
        llmService.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Think about goroutines.", InputTokensUsed = 75, ContextWindowSize = 200_000 });

        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(AiProvider.Anthropic).Returns(llmService);

        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns("system prompt");

        var service = BuildService(factory: factory, store: store, templates: templates);

        var response = await service.GetGuidanceAsync(session.SessionId, "I'm stuck", null, false, CancellationToken.None);

        Assert.Equal("Think about goroutines.", response.Response);
        Assert.Equal(75,       response.ContextTokensUsed);
        Assert.Equal(200_000,  response.ContextWindowSize);
    }

    [Fact]
    public async Task GetGuidanceAsync_AppendsUserAndAssistantMessagesToSession()
    {
        var session = new ProblemSession
        {
            Language           = Language.CSharp,
            Provider           = AiProvider.Anthropic,
            ProblemDescription = "Sort a list.",
            StarterCode        = "void Sort() {}"
        };

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        var llmService = Substitute.For<ITutoringLlmService>();
        llmService.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Try QuickSort.", InputTokensUsed = 20, ContextWindowSize = 200_000 });

        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns("system prompt");

        var service = BuildService(factory: factory, store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "What algorithm should I use?", null, false, CancellationToken.None);

        store.Received(1).Set(Arg.Is<ProblemSession>(s =>
            s.Messages.Count == 2 &&
            s.Messages[0].Role    == MessageRole.User      && s.Messages[0].Content == "What algorithm should I use?" &&
            s.Messages[1].Role    == MessageRole.Assistant && s.Messages[1].Content == "Try QuickSort."));
    }

    [Fact]
    public async Task GetGuidanceAsync_PassesPriorHistoryToLlm()
    {
        var session = new ProblemSession
        {
            Language           = Language.Rust,
            Provider           = AiProvider.Anthropic,
            ProblemDescription = "Implement a stack.",
            StarterCode        = "struct Stack {}"
        };

        // Pre-populate with one prior exchange
        session.Messages.Add(new ChatMessage { Role = MessageRole.User,      Content = "first question" });
        session.Messages.Add(new ChatMessage { Role = MessageRole.Assistant, Content = "first answer"   });

        var store = Substitute.For<ISessionStore<ProblemSession>>();
        store.Get(Arg.Any<string>()).Returns(session);

        // Snapshot the history contents at call time — session.Messages is the same list object
        // and gets mutated (assistant message appended) immediately after the LLM returns.
        // Arg.Is would evaluate against the post-mutation list (4 items, not 3).
        List<string>? capturedContents = null;
        var llmService = Substitute.For<ITutoringLlmService>();
        llmService
            .When(x => x.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>()))
            .Do(call => capturedContents = call.Arg<IReadOnlyList<ChatMessage>>().Select(m => m.Content).ToList());
        llmService.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "second answer", InputTokensUsed = 30, ContextWindowSize = 200_000 });

        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns("system prompt");

        var service = BuildService(factory: factory, store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "second question", null, false, CancellationToken.None);

        // LLM receives 2 prior messages + the new user message = 3 total
        Assert.NotNull(capturedContents);
        Assert.Equal(3,               capturedContents.Count);
        Assert.Equal("first question", capturedContents[0]);
        Assert.Equal("first answer",   capturedContents[1]);
        Assert.Equal("second question", capturedContents[2]);
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

        var llmService = Substitute.For<ITutoringLlmService>();
        llmService.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Good start.", InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns("system prompt");

        var service = BuildService(factory: factory, store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "review my code", "const solve = () => 42;", false, CancellationToken.None);

        templates.Received(1).GuidanceSystemPrompt(
            Language.TypeScript,
            Arg.Any<string>(),
            Arg.Any<string>(),
            "const solve = () => 42;",
            false);
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

        var llmService = Substitute.For<ITutoringLlmService>();
        llmService.GetGuidanceAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatMessage>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Your output looks correct.", InputTokensUsed = 15, ContextWindowSize = 200_000 });

        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.GuidanceSystemPrompt(Arg.Any<Language>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>())
            .Returns("code analysis system prompt");

        var service = BuildService(factory: factory, store: store, templates: templates);

        await service.GetGuidanceAsync(session.SessionId, "I ran my code", null, isCodeAnalysis: true, CancellationToken.None);

        templates.Received(1).GuidanceSystemPrompt(
            Language.Java,
            Arg.Any<string>(),
            Arg.Any<string>(),
            null,
            true);
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
