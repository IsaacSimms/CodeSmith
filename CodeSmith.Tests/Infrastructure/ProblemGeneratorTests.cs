// == Problem Generator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

[Collection("CodeSmithTelemetry")] // span capture is process-global — see ActivityCapture
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

    private static (ILlmService, ILlmServiceFactory) LlmReturning(string content)
    {
        var llmService = Substitute.For<ILlmService>();
        llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = content, InputTokensUsed = 10, ContextWindowSize = 200_000 });
        var factory = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);
        return (llmService, factory);
    }

    private static ITutoringPromptTemplates TemplatesReturning(
        ProblemFocus focus = ProblemFocus.Standard,
        ProblemTopic topic = ProblemTopic.ArraysAndStrings,
        string languageLabel = "Python")
    {
        var templates = Substitute.For<ITutoringPromptTemplates>();
        templates.ProblemGeneration(Arg.Any<ProblemSpec>())
            .Returns(new ProblemGenerationRequest("sys", "user", focus, topic, languageLabel));
        return templates;
    }

    private static ProblemSpec Spec(
        Difficulty difficulty = Difficulty.Easy,
        Language language     = Language.Python,
        AiProvider provider   = AiProvider.Anthropic,
        ProblemFocus focus    = ProblemFocus.Random,
        ProblemTopic topic    = ProblemTopic.Random)
        => new(difficulty, language, provider, focus, topic);

    // Wires an ILlmService whose StreamAsync pushes the given delta sequences (one per attempt) and
    // returns the concatenated content, with per-attempt truncation flags.
    private static (ILlmService, ILlmServiceFactory) LlmStreaming(params (string[] Deltas, bool WasTruncated)[] attempts)
    {
        var llmService = Substitute.For<ILlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);

        var attempt = 0;
        llmService.StreamAsync(Arg.Any<CompletionRequest>(), Arg.Any<Func<string, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var (deltas, wasTruncated) = attempts[Math.Min(attempt++, attempts.Length - 1)];
                var onDelta = callInfo.Arg<Func<string, CancellationToken, Task>>();
                foreach (var delta in deltas)
                    await onDelta(delta, CancellationToken.None);
                return new LlmResponse { Content = string.Concat(deltas), WasTruncated = wasTruncated, InputTokensUsed = 10, ContextWindowSize = 200_000 };
            });

        return (llmService, factory);
    }

    // == Happy Path == //

    [Fact]
    public async Task GenerateAsync_ReturnsDescriptionAndStarterCode()
    {
        var (_, factory) = LlmReturning("raw");
        factory.Get(AiProvider.Anthropic)
            .CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "raw", InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("Find the nth Fibonacci number.", "def fib(n): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        var generated = await generator.GenerateAsync(Spec(Difficulty.Hard, Language.Python), CancellationToken.None);

        Assert.Equal("Find the nth Fibonacci number.", generated.Description);
        Assert.Equal("def fib(n): pass",               generated.StarterCode);
    }

    [Fact]
    public async Task GenerateAsync_CallsTemplatesWithCorrectDifficultyAndLanguage()
    {
        var (_, factory) = LlmReturning("raw");
        var templates = TemplatesReturning(languageLabel: "Go");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "func solve() {}"));

        var generator = BuildGenerator(templates: templates, factory: factory, parser: parser);

        await generator.GenerateAsync(Spec(Difficulty.Medium, Language.Go), CancellationToken.None);

        templates.Received(1).ProblemGeneration(Arg.Is<ProblemSpec>(s => s.Difficulty == Difficulty.Medium && s.Language == Language.Go));
    }

    [Fact]
    public async Task GenerateAsync_RoutesLlmCallThroughCorrectProvider()
    {
        var (_, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "const solve = () => {};"));

        var generator = BuildGenerator(templates: TemplatesReturning(languageLabel: "TypeScript"), factory: factory, parser: parser);

        await generator.GenerateAsync(Spec(Difficulty.Easy, Language.TypeScript, AiProvider.OpenAi), CancellationToken.None);

        factory.Received(1).Get(AiProvider.OpenAi);
    }

    [Fact]
    public async Task GenerateAsync_RoutesLlmCallThroughCorrectProvider_Xai()
    {
        var (_, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "fn main() {}"));

        var generator = BuildGenerator(templates: TemplatesReturning(languageLabel: "Rust"), factory: factory, parser: parser);

        await generator.GenerateAsync(Spec(Difficulty.Medium, Language.Rust, AiProvider.Xai), CancellationToken.None);

        factory.Received(1).Get(AiProvider.Xai);
    }

    // == Truncation-Retry Tests == //

    [Fact]
    public async Task GenerateAsync_WhenLlmReturnsTruncated_RetriesWithHintAndSucceeds()
    {
        var llmService = Substitute.For<ILlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);

        // First call: truncated. Second call: valid response.
        llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse { Content = "",    WasTruncated = true,  InputTokensUsed = 5,  ContextWindowSize = 200_000 },
                new LlmResponse { Content = "raw", WasTruncated = false, InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("A complete description.", "def solve(): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        var generated = await generator.GenerateAsync(Spec(), CancellationToken.None);

        Assert.Equal("A complete description.", generated.Description);
        Assert.Equal("def solve(): pass",       generated.StarterCode);

        // Second call must carry the truncation hint
        await llmService.Received(1).CompleteAsync(Arg.Is<CompletionRequest>(r => r.Messages.Any(m => m.Content.Contains("cut off due to token limits"))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WhenAllAttemptsAreTruncated_ThrowsAiServiceException()
    {
        var llmService = Substitute.For<ILlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);

        llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "", WasTruncated = true, InputTokensUsed = 5, ContextWindowSize = 200_000 });

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory);

        await Assert.ThrowsAsync<AiServiceException>(
            () => generator.GenerateAsync(Spec(), CancellationToken.None));

        // 3 attempts total (MaxRetries = 2)
        await llmService.Received(3).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
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

        var generated = await generator.GenerateAsync(Spec(), CancellationToken.None);

        Assert.Equal("A valid description", generated.Description);
        Assert.Equal("def solve(): pass",   generated.StarterCode);
        await llmService.Received(2).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WhenAllAttemptsProduceMalformedOutput_ThrowsAiServiceException()
    {
        var (llmService, factory) = LlmReturning("bad");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("", "")); // Always empty

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        await Assert.ThrowsAsync<AiServiceException>(
            () => generator.GenerateAsync(Spec(), CancellationToken.None));

        // 3 attempts total: attempt 0, 1, 2 (MaxParseRetries = 2)
        await llmService.Received(3).CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>());
    }

    // == Token Budget Pin == //

    [Fact]
    public async Task GenerateAsync_RequestsFourThousandMaxTokens()
    {
        var (llmService, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "def solve(): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        await generator.GenerateAsync(Spec(Difficulty.Hard), CancellationToken.None);

        // 4000 keeps truncation retries rare; reserve estimates against it but settle refunds to actuals
        await llmService.Received(1).CompleteAsync(Arg.Is<CompletionRequest>(r => r.MaxTokens == 4000), Arg.Any<CancellationToken>());
    }

    // == Telemetry Span Tests == //

    [Fact]
    public async Task GenerateAsync_EmitsOneAttemptSpanPerAttempt_TaggedWithOutcome()
    {
        using var capture = new ActivityCapture();

        var llmService = Substitute.For<ILlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);

        // First attempt truncated, second succeeds — the silent retry must be visible in traces
        llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse { Content = "",    WasTruncated = true,  InputTokensUsed = 5,  ContextWindowSize = 200_000 },
                new LlmResponse { Content = "raw", WasTruncated = false, InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("A complete description.", "def solve(): pass"));

        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: parser);

        await generator.GenerateAsync(Spec(), CancellationToken.None);

        var attempts = capture.All("problem.generation.attempt");
        Assert.Equal(2, attempts.Count);

        Assert.Equal(1,      attempts[0].GetTagItem("codesmith.attempt"));
        Assert.Equal(true,   attempts[0].GetTagItem("codesmith.truncated"));

        Assert.Equal(2,      attempts[1].GetTagItem("codesmith.attempt"));
        Assert.Equal(false,  attempts[1].GetTagItem("codesmith.truncated"));
        Assert.Equal(true,   attempts[1].GetTagItem("codesmith.parse_complete"));
    }

    // == Streaming Generation == //

    [Fact]
    public async Task StreamGenerateAsync_StreamsOnlyDescriptionText_EvenWithMarkersSplitAcrossDeltas()
    {
        // Both markers arrive split across delta boundaries; only the text between them may stream,
        // and the starter code must never reach the description callback.
        var (_, factory) = LlmStreaming((["DESCRI", "PTION: Two ", "Sum problem\nSTARTER", "_CODE: def x():"], false));
        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: new ProblemResponseParser());

        var streamed = new List<string>();
        var generated = await generator.StreamGenerateAsync(
            Spec(provider: AiProvider.Xai),
            (text, _) => { streamed.Add(text); return Task.CompletedTask; },
            _ => Task.CompletedTask);

        Assert.Equal("Two Sum problem", string.Concat(streamed).Trim());
        Assert.DoesNotContain(streamed, s => s.Contains("def x()"));
        Assert.DoesNotContain(streamed, s => s.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Two Sum problem", generated.Description);
        Assert.Equal("def x():", generated.StarterCode);
    }

    [Fact]
    public async Task StreamGenerateAsync_OnTruncatedFirstAttempt_SignalsResetThenRestreams()
    {
        // Attempt 1 truncates after streaming visible text; the consumer must get a reset before
        // attempt 2's text so stale attempt-1 description can be cleared (the locked retry UX).
        var (_, factory) = LlmStreaming(
            (["DESCRIPTION: half a prob"], true),
            (["DESCRIPTION: Whole problem\nSTARTER_CODE: def y():"], false));
        var generator = BuildGenerator(templates: TemplatesReturning(), factory: factory, parser: new ProblemResponseParser());

        var events = new List<string>();   // interleaved log proves reset lands between the attempts
        var generated = await generator.StreamGenerateAsync(
            Spec(provider: AiProvider.Xai),
            (text, _) => { events.Add("delta:" + text); return Task.CompletedTask; },
            _ => { events.Add("reset"); return Task.CompletedTask; });

        Assert.Equal("Whole problem", generated.Description);
        Assert.Equal("def y():", generated.StarterCode);

        var resetIndex = events.IndexOf("reset");
        Assert.True(resetIndex > 0, "reset must come after attempt 1's deltas");
        Assert.Contains(events[..resetIndex],  e => e.Contains("half a prob"));
        Assert.Contains(events[(resetIndex + 1)..], e => e.Contains("Whole problem"));
    }

    // == Resolved Variety == //

    [Fact]
    public async Task GenerateAsync_ReturnsResolvedFocusAndTopicOnGeneratedProblem()
    {
        var (_, factory) = LlmReturning("raw");

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse(Arg.Any<string>()).Returns(("Description.", "def solve(): pass"));

        var templates = TemplatesReturning(focus: ProblemFocus.Refactoring, topic: ProblemTopic.StateMachines);
        var generator = BuildGenerator(templates: templates, factory: factory, parser: parser);

        // The caller asked for Random; what comes back is what was actually requested of the provider
        var generated = await generator.GenerateAsync(Spec(), CancellationToken.None);

        Assert.Equal(ProblemFocus.Refactoring,  generated.Focus);
        Assert.Equal(ProblemTopic.StateMachines, generated.Topic);
    }

    [Fact]
    public async Task GenerateAsync_WhenTruncationRetryOccurs_ResolvesVarietyOnlyOnce()
    {
        // Resolution must sit outside the attempt loop: a retry re-asks for the same problem shape
        // rather than silently rolling a new focus and topic mid-generation.
        var llmService = Substitute.For<ILlmService>();
        var factory    = Substitute.For<ILlmServiceFactory>();
        factory.Get(Arg.Any<AiProvider>()).Returns(llmService);

        llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                new LlmResponse { Content = "",    WasTruncated = true,  InputTokensUsed = 5,  ContextWindowSize = 200_000 },
                new LlmResponse { Content = "raw", WasTruncated = false, InputTokensUsed = 10, ContextWindowSize = 200_000 });

        var parser = Substitute.For<IProblemResponseParser>();
        parser.Parse("raw").Returns(("A complete description.", "def solve(): pass"));

        var templates = TemplatesReturning(focus: ProblemFocus.BugFix, topic: ProblemTopic.TreesAndGraphs);
        var generator = BuildGenerator(templates: templates, factory: factory, parser: parser);

        var generated = await generator.GenerateAsync(Spec(), CancellationToken.None);

        templates.Received(1).ProblemGeneration(Arg.Any<ProblemSpec>());
        Assert.Equal(ProblemFocus.BugFix,          generated.Focus);
        Assert.Equal(ProblemTopic.TreesAndGraphs,  generated.Topic);
    }
}
