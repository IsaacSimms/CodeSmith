// == Test Input Generator Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Core.Models.PromptLab;
using CodeSmith.Infrastructure.Services.PromptLab;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure.PromptLab;

public class TestInputGeneratorTests
{
    private readonly ILlmServiceFactory          _factory    = Substitute.For<ILlmServiceFactory>();
    private readonly ILlmService                _llmService = Substitute.For<ILlmService>();
    private readonly ILogger<TestInputGenerator> _logger     = Substitute.For<ILogger<TestInputGenerator>>();
    private readonly TestInputGenerator           _generator;

    public TestInputGeneratorTests()
    {
        _factory.Get(Arg.Any<AiProvider>()).Returns(_llmService);
        _generator = new TestInputGenerator(_factory, _logger);
    }

    // == Valid Generation == //

    [Fact]
    public async Task GenerateAsync_ValidJsonArray_ReturnsFourInputs()
    {
        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = ValidFourInputsJson() });

        var result = await _generator.GenerateAsync(MakeChallenge(), AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task GenerateAsync_ValidJson_MapsFieldsCorrectly()
    {
        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = ValidFourInputsJson() });

        var result = await _generator.GenerateAsync(MakeChallenge(), AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal("gen-1", result[0].InputId);
        Assert.Equal("First Input", result[0].Label);
        Assert.Equal("message one", result[0].UserMessage);
        Assert.Equal("expected one", result[0].ExpectedBehavior);
    }

    [Fact]
    public async Task GenerateAsync_JsonWrappedInMarkdownFences_ParsesSuccessfully()
    {
        var fenced = $"```json\n{ValidFourInputsJson()}\n```";
        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = fenced });

        var result = await _generator.GenerateAsync(MakeChallenge(), AiProvider.Anthropic, CancellationToken.None);

        Assert.Equal(4, result.Count);
    }

    // == Error Cases == //

    [Fact]
    public async Task GenerateAsync_WrongCount_ThrowsInvalidOperationException()
    {
        var threeInputsJson = """
            [
                {"inputId":"gen-1","label":"A","userMessage":"m","expectedBehavior":"e"},
                {"inputId":"gen-2","label":"B","userMessage":"m","expectedBehavior":"e"},
                {"inputId":"gen-3","label":"C","userMessage":"m","expectedBehavior":"e"}
            ]
            """;

        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = threeInputsJson });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _generator.GenerateAsync(MakeChallenge(), AiProvider.Anthropic, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_MalformedJson_Throws()
    {
        _llmService.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "this is not json" });

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _generator.GenerateAsync(MakeChallenge(), AiProvider.Anthropic, CancellationToken.None));
    }

    // == Helpers == //

    private static string ValidFourInputsJson() => """
        [
            {"inputId":"gen-1","label":"First Input","userMessage":"message one","expectedBehavior":"expected one"},
            {"inputId":"gen-2","label":"Second Input","userMessage":"message two","expectedBehavior":"expected two"},
            {"inputId":"gen-3","label":"Third Input","userMessage":"message three","expectedBehavior":"expected three"},
            {"inputId":"gen-4","label":"Fourth Input","userMessage":"message four","expectedBehavior":"expected four"}
        ]
        """;

    private static Challenge MakeChallenge() => new()
    {
        ChallengeId        = "test-01",
        Title              = "Test Challenge",
        Description        = "Do the thing.",
        Category           = ChallengeCategory.OutputFormatControl,
        LockedSystemPrompt = "You are a helpful assistant.",
        TestInputs         = [],
        EditableFields     = [],
        Rubric             = []
    };
}
