// == Anthropic LLM Adapter Tests == //
using System.Net;
using System.Text.Json;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// Pins the Anthropic adapter's translation contract at the HTTP seam: the outgoing request
/// (tier→model, max_tokens, system placement, role mapping) and the response mapping
/// (configured-model stamping, token counts, truncation flag, content concatenation, error wrapping).
/// </summary>
public class AnthropicLlmServiceTests
{
    private const string SystemPrompt = "You are a tutor.";
    private const string Feature      = "Tutoring:Guidance";

    // == Helpers == //

    private static AnthropicOptions DefaultOptions() => new()
    {
        ApiKey        = "test-key",
        AccurateModel = "claude-sonnet-4-6",
        FastModel     = "claude-haiku-4-5-20251001",
        ContextWindow = 200_000
    };

    private static AnthropicLlmService CreateService(CapturingHttpHandler handler)
        => new(Options.Create(DefaultOptions()),
               Substitute.For<ILogger<AnthropicLlmService>>(),
               new HttpClient(handler));

    private static CompletionRequest SingleTurn(ModelTier tier = ModelTier.Fast)
        => CompletionRequest.SingleTurn(SystemPrompt, "Hello", tier, 512, Feature);

    // Canned wire response. "model" is deliberately NOT a configured model name so tests catch an
    // adapter that stamps the served model instead of the configured one (the pricing invariant).
    private static string ResponseJson(string contentBlocks = """[{"type":"text","text":"Hello!"}]""",
                                       string stopReason = "end_turn",
                                       int inputTokens = 12,
                                       int outputTokens = 7) => $$"""
        {
          "id":"msg_01","type":"message","role":"assistant","model":"served-model-name",
          "content":{{contentBlocks}},
          "stop_reason":"{{stopReason}}","stop_sequence":null,
          "usage":{"input_tokens":{{inputTokens}},"output_tokens":{{outputTokens}}}
        }
        """;

    private static CapturingHttpHandler OkHandler(string? body = null)
        => new(HttpStatusCode.OK, body ?? ResponseJson());

    // == Outgoing request: tier→model, endpoint == //

    [Theory]
    [InlineData(ModelTier.Accurate, "claude-sonnet-4-6")]
    [InlineData(ModelTier.Fast,     "claude-haiku-4-5-20251001")]
    public async Task CompleteAsync_SendsConfiguredModelForTier(ModelTier tier, string expectedModel)
    {
        var handler = OkHandler();

        await CreateService(handler).CompleteAsync(SingleTurn(tier));

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(expectedModel, body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task CompleteAsync_TargetsAnthropicMessagesEndpoint()
    {
        var handler = OkHandler();

        await CreateService(handler).CompleteAsync(SingleTurn());

        Assert.Equal("api.anthropic.com", handler.LastRequest!.RequestUri!.Host);
        Assert.EndsWith("/messages", handler.LastRequest.RequestUri.AbsolutePath);
    }

    // == Outgoing request: system prompt, max_tokens, role-mapped ordered messages == //

    [Fact]
    public async Task CompleteAsync_SendsSystemPromptMaxTokensAndRoleMappedMessagesInOrder()
    {
        var handler = OkHandler();
        var request = new CompletionRequest
        {
            SystemPrompt = SystemPrompt,
            Messages =
            [
                new ChatMessage { Role = MessageRole.User,      Content = "first"  },
                new ChatMessage { Role = MessageRole.Assistant, Content = "second" },
                new ChatMessage { Role = MessageRole.User,      Content = "third"  }
            ],
            Tier      = ModelTier.Fast,
            MaxTokens = 512,
            Feature   = Feature
        };

        await CreateService(handler).CompleteAsync(request);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;

        Assert.Equal(SystemPrompt, root.GetProperty("system").GetString());
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());

        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(3, messages.Count);
        Assert.Collection(messages,
            m => { Assert.Equal("user",      m.GetProperty("role").GetString()); Assert.Equal("first",  m.GetProperty("content").GetString()); },
            m => { Assert.Equal("assistant", m.GetProperty("role").GetString()); Assert.Equal("second", m.GetProperty("content").GetString()); },
            m => { Assert.Equal("user",      m.GetProperty("role").GetString()); Assert.Equal("third",  m.GetProperty("content").GetString()); });
    }

    // == Response mapping: configured-model stamping, usage, context window == //

    [Fact]
    public async Task CompleteAsync_StampsConfiguredModelAndMapsUsage()
    {
        var handler = OkHandler();   // wire response says "served-model-name"

        var result = await CreateService(handler).CompleteAsync(SingleTurn(ModelTier.Fast));

        Assert.Equal("claude-haiku-4-5-20251001", result.Model);   // configured name, never the served one — pricing keys off this
        Assert.Equal("Hello!", result.Content);
        Assert.Equal(12, result.InputTokensUsed);
        Assert.Equal(7,  result.OutputTokensUsed);
        Assert.Equal(200_000, result.ContextWindowSize);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public async Task CompleteAsync_MultipleTextBlocks_ConcatenatesContent()
    {
        var handler = OkHandler(ResponseJson(contentBlocks: """[{"type":"text","text":"Part1 "},{"type":"text","text":"Part2"}]"""));

        var result = await CreateService(handler).CompleteAsync(SingleTurn());

        Assert.Equal("Part1 Part2", result.Content);
    }

    [Fact]
    public async Task CompleteAsync_StopReasonMaxTokens_SetsWasTruncated()
    {
        var handler = OkHandler(ResponseJson(stopReason: "max_tokens"));

        var result = await CreateService(handler).CompleteAsync(SingleTurn());

        Assert.True(result.WasTruncated);
    }

    // == Error modes == //

    [Fact]
    public async Task CompleteAsync_ApiFailure_WrapsAiServiceExceptionNamingFeature()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.BadRequest,
            """{"type":"error","error":{"type":"invalid_request_error","message":"bad request"}}""");

        var ex = await Assert.ThrowsAsync<AiServiceException>(
            () => CreateService(handler).CompleteAsync(SingleTurn()));

        Assert.Contains(Feature, ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task CompleteAsync_CancelledToken_SurfacesOperationCanceledException()
    {
        // Cancellation must NOT be wrapped into AiServiceException (502) — the middleware maps
        // OperationCanceledException to 499, and GuidanceConversation relies on the passthrough.
        var handler = OkHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(handler).CompleteAsync(SingleTurn(), cts.Token));
    }
}
