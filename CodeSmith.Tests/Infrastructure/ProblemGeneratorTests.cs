// == Problem Generator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

public class ProblemGeneratorTests
{
    // == Helpers == //

    private static ProblemGenerator BuildGenerator(
        ITutoringPromptTemplates?  templates = null,
        ILlmServiceFactory?        factory   = null,
        IProblemResponseParser?    parser    = null,
        ILogger<ProblemGenerator>? logger    = null)
        => new(
            templates ?? Substitute.For<ITutoringPromptTemplates>(),
            factory   ?? Substitute.For<ILlmServiceFactory>(),
            parser    ?? Substitute.For<IProblemResponseParser>(),
            logger    ?? Substitute.For<ILogger<ProblemGenerator>>());

    private static (ITutoringLlmService, ILlmServiceFactory) LlmReturning(string content)
    {
        var llmService = Substitute.For<ITutoringLlmService>();
        llmService.GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = content, InputTokensUsed = 10, ContextWindowSize = 200_000 });
        var factory = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);
        return (llmService, factory);
    }

    private static ITutoringPromptTemplates TemplatesReturning(string category = "arrays", string angle = "Standard implementation", string languageLabel = "Python")
    {
        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.ProblemGeneration(Arg.Any<Difficulty>(), Arg.Any<Language>())
            .Returns(new ProblemGenerationRequest("sys", "user", category, angle, languageLabel));
        return templates;
    }

    // == Happy Path == //

    [Fact]
    public async Task GenerateAsync_ReturnsDescriptionAndStarterCode()
    {
        var (_, factory) = LlmReturning("raw");
        factory.GetLlmService<ITutoringLlmService>(AiProvider.Anthropic)
            .GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "raw", InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("Find the nth Fibonacci number.", "def fib(n): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        var (description, starterCode) = await generator.GenerateAsync(Difficulty.Hard, Language.Python, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("Find the nth Fibonacci number.", description);
        Assert.Equal("def fib(n): pass",               starterCode);
    }

    [Fact]
    public async Task GenerateAsync_CallsTemplatesWithCorrectDifficultyAndLanguage()
    {
        var (_, factory) = LlmReturning("raw");
        var templates = TemplatesReturning(languageLabel: "Go");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "func solve() {}"));

        var generator = BuildGenerator(templates: templates, factory: factory, parser: parser);

        await generator.GenerateAsync(Difficulty.Medium, Language.Go, AiProvider.Anthropic, CancellationToken.None);

        templates.Received(1).ProblemGeneration(Difficulty.Medium, Language.Go);
    }

    [Fact]
    public async Task GenerateAsync_RoutesLlmCallThroughCorrectProvider()
    {
        var (_, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "const solve = () => {};"));

        var generator = BuildGenerator(templates: TemplatesReturning(languageLabel: "TypeScript"), factory: factory, parser: parser);

        await generator.GenerateAsync(Difficulty.Easy, Language.TypeScript, AiProvider.OpenAi, CancellationToken.None);

        factory.Received(1).GetLlmService<ITutoringLlmService>(AiProvider.OpenAi);
    }

    [Fact]
    public async Task GenerateAsync_RoutesLlmCallThroughCorrectProvider_Xai()
    {
        var (_, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "fn main() {}"));

        var generator = BuildGenerator(templates: TemplatesReturning(languageLabel: "Rust"), factory: factory, parser: parser);

        await generator.GenerateAsync(Difficulty.Medium, Language.Rust, AiProvider.Xai, CancellationToken.None);

        factory.Received(1).GetLlmService<ITutoringLlmService>(AiProvider.Xai);
    }

    // == Truncation-Retry Tests == //

    [Fact]
    public async Task GenerateAsync_WhenLlmReturnsTruncated_RetriesWithHintAndSucceeds()
    {
        var llmService = Substitute.For<ITutoringLlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        // First call: truncated. Second call: valid response.
        llmService.GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse { Content = "",    WasTruncated = true,  InputTokensUsed = 5,  ContextWindowSize = 200_000 },
                new LlmResponse { Content = "raw", WasTruncated = false, InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("A complete description.", "def solve(): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        var (description, starterCode) = await generator.GenerateAsync(Difficulty.Easy, Language.Python, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("A complete description.", description);
        Assert.Equal("def solve(): pass",       starterCode);

        // Second call must carry the truncation hint
        await llmService.Received(1).GenerateProblemAsync(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("cut off due to token limits")), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WhenAllAttemptsAreTruncated_ThrowsAiServiceException()
    {
        var llmService = Substitute.For<ITutoringLlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.GetLlmService<ITutoringLlmService>(Arg.Any<AiProvider>()).Returns(llmService);

        llmService.GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "", WasTruncated = true, InputTokensUsed = 5, ContextWindowSize = 200_000 });

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory);

        await Assert.ThrowsAsync<AiServiceException>(
            () => generator.GenerateAsync(Difficulty.Easy, Language.Python, AiProvider.Anthropic, CancellationToken.None));

        // 3 attempts total (MaxRetries = 2)
        await llmService.Received(3).GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    // == Parse-Retry Tests == //

    [Fact]
    public async Task GenerateAsync_WhenFirstParseIsEmpty_RetriesAndSucceeds()
    {
        var (llmService, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        // First call returns empty; second returns a valid problem
        parser.Parse(Arg.Any<string>())
            .Returns(("", ""), ("A valid description", "def solve(): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        var (description, starterCode) = await generator.GenerateAsync(Difficulty.Easy, Language.Python, AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("A valid description", description);
        Assert.Equal("def solve(): pass",   starterCode);
        await llmService.Received(2).GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WhenAllAttemptsProduceMalformedOutput_ThrowsAiServiceException()
    {
        var (llmService, factory) = LlmReturning("bad");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("", "")); // Always empty

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        await Assert.ThrowsAsync<AiServiceException>(
            () => generator.GenerateAsync(Difficulty.Easy, Language.Python, AiProvider.Anthropic, CancellationToken.None));

        // 3 attempts total: attempt 0, 1, 2 (MaxParseRetries = 2)
        await llmService.Received(3).GenerateProblemAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
