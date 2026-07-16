// == OpenAI-Compatible LLM Adapter Tests == //
using System.Net;
using System.Text.Json;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CodeSmith.Tests.Infrastructure;

/// <summary>
/// Pins the OpenAI-compatible adapter's translation contract at the HTTP seam for both providers it
/// drives (OpenAI default endpoint, xAI custom endpoint): outgoing request shape (tier→model,
/// endpoint routing, system-first message ordering, max tokens) and response mapping
/// (configured-model stamping, token counts, truncation flag, error wrapping).
/// </summary>
public class OpenAiCompatibleLlmServiceTests
{
    private const string SystemPrompt  = "You are a tutor.";
    private const string Feature       = "PromptLab:Evaluate";
    private const string AccurateModel = "gpt-4.1";
    private const string FastModel     = "gpt-4.1-mini";
    private const string XaiEndpoint   = "https://api.x.ai/v1";

    // == Helpers == //

    private static OpenAiCompatibleLlmService CreateService(
        CapturingHttpHandler handler,
        AiProvider provider = AiProvider.OpenAi,
        string? endpoint = null)
        => new(provider, "test-key", AccurateModel, FastModel, 1_047_576, endpoint,
               Substitute.For<ILogger<OpenAiCompatibleLlmService>>(),
               new HttpClient(handler));

    private static CompletionRequest SingleTurn(ModelTier tier = ModelTier.Fast)
        => CompletionRequest.SingleTurn(SystemPrompt, "Hello", tier, 512, Feature);

    // "model" is deliberately a name we never configure, so a test fails if the adapter stamps the
    // served model instead of the configured one (the pricing invariant).
    private static string ResponseJson(string finishReason = "stop",
                                       int promptTokens = 10,
                                       int completionTokens = 4) => $$"""
        {
          "id":"chatcmpl-1","object":"chat.completion","created":1700000000,"model":"served-model-name",
          "choices":[{"index":0,"message":{"role":"assistant","content":"Hi"},"finish_reason":"{{finishReason}}"}],
          "usage":{"prompt_tokens":{{promptTokens}},"completion_tokens":{{completionTokens}},"total_tokens":{{promptTokens + completionTokens}}}
        }
        """;

    private static CapturingHttpHandler OkHandler(string? body = null)
        => new(HttpStatusCode.OK, body ?? ResponseJson());

    // The SDK has renamed this wire field before (max_tokens → max_completion_tokens); accept either
    // name but pin the value so an SDK upgrade can't silently drop the output budget.
    private static int ReadMaxTokens(JsonElement root)
        => root.TryGetProperty("max_completion_tokens", out var v) ? v.GetInt32()
         : root.GetProperty("max_tokens").GetInt32();

    // == Endpoint routing == //

    [Fact]
    public async Task CompleteAsync_NullEndpoint_TargetsOpenAiDefault()
    {
        var handler = OkHandler();

        await CreateService(handler).CompleteAsync(SingleTurn());

        Assert.Equal("api.openai.com", handler.LastRequest!.RequestUri!.Host);
        Assert.EndsWith("/chat/completions", handler.LastRequest.RequestUri.AbsolutePath);
    }

    [Fact]
    public async Task CompleteAsync_XaiEndpoint_TargetsConfiguredBaseUrl()
    {
        var handler = OkHandler();

        await CreateService(handler, AiProvider.Xai, XaiEndpoint).CompleteAsync(SingleTurn());

        Assert.Equal("api.x.ai", handler.LastRequest!.RequestUri!.Host);
        Assert.EndsWith("/chat/completions", handler.LastRequest.RequestUri.AbsolutePath);
    }

    // == Outgoing request: tier→model, system-first ordering, max tokens == //

    [Theory]
    [InlineData(ModelTier.Accurate, AccurateModel)]
    [InlineData(ModelTier.Fast,     FastModel)]
    public async Task CompleteAsync_SendsConfiguredModelForTier(ModelTier tier, string expectedModel)
    {
        var handler = OkHandler();

        await CreateService(handler).CompleteAsync(SingleTurn(tier));

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(expectedModel, body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task CompleteAsync_SendsSystemFirstThenRoleMappedMessagesInOrder()
    {
        var handler = OkHandler();
        var request = new CompletionRequest
        {
            SystemPrompt = SystemPrompt,
            Messages =
            [
                new ChatMessage { Role = MessageRole.User,      Content = "first"  },
                new ChatMessage { Role = MessageRole.Assistant, Content = "second" }
            ],
            Tier      = ModelTier.Fast,
            MaxTokens = 512,
            Feature   = Feature
        };

        await CreateService(handler).CompleteAsync(request);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;

        Assert.Equal(512, ReadMaxTokens(root));

        var messages = root.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(3, messages.Count);   // system + the two turns
        Assert.Collection(messages,
            m => { Assert.Equal("system",    m.GetProperty("role").GetString()); Assert.Equal(SystemPrompt, ReadContent(m)); },
            m => { Assert.Equal("user",      m.GetProperty("role").GetString()); Assert.Equal("first",      ReadContent(m)); },
            m => { Assert.Equal("assistant", m.GetProperty("role").GetString()); Assert.Equal("second",     ReadContent(m)); });
    }

    // Chat message content may serialize as a bare string or as [{type:"text",text:...}] parts
    private static string ReadContent(JsonElement message)
    {
        var content = message.GetProperty("content");
        return content.ValueKind == JsonValueKind.String
            ? content.GetString()!
            : content.EnumerateArray().First().GetProperty("text").GetString()!;
    }

    // == Response mapping == //

    [Fact]
    public async Task CompleteAsync_StampsConfiguredModelAndMapsUsage()
    {
        var handler = OkHandler();   // wire response says "served-model-name"

        var result = await CreateService(handler).CompleteAsync(SingleTurn(ModelTier.Accurate));

        Assert.Equal(AccurateModel, result.Model);   // configured name, never the served one — pricing keys off this
        Assert.Equal("Hi", result.Content);
        Assert.Equal(10, result.InputTokensUsed);
        Assert.Equal(4,  result.OutputTokensUsed);
        Assert.Equal(1_047_576, result.ContextWindowSize);
        Assert.False(result.WasTruncated);
    }

    [Fact]
    public async Task CompleteAsync_FinishReasonLength_SetsWasTruncated()
    {
        var handler = OkHandler(ResponseJson(finishReason: "length"));

        var result = await CreateService(handler).CompleteAsync(SingleTurn());

        Assert.True(result.WasTruncated);
    }

    // == Error modes == //

    [Fact]
    public async Task CompleteAsync_ApiFailure_WrapsAiServiceExceptionNamingProviderAndFeature()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.BadRequest,
            """{"error":{"message":"bad request","type":"invalid_request_error"}}""");

        var ex = await Assert.ThrowsAsync<AiServiceException>(
            () => CreateService(handler, AiProvider.Xai, XaiEndpoint).CompleteAsync(SingleTurn()));

        Assert.Contains("Xai", ex.Message);
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

    // == Transport config: no auto-retry, explicit timeout == //

    [Fact]
    public async Task CompleteAsync_OnRetryableServerError_SendsExactlyOneRequest()
    {
        // A transport-level retry re-runs a metered completion — it burns real provider cost
        // invisibly and multiplies worst-case latency, so the pipeline's default retries must stay off.
        var handler = new CapturingHttpHandler(HttpStatusCode.ServiceUnavailable,
            """{"error":{"message":"overloaded","type":"server_error"}}""");

        await Assert.ThrowsAsync<AiServiceException>(
            () => CreateService(handler).CompleteAsync(SingleTurn()));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void Ctor_SetsExplicitNetworkTimeout()
    {
        // Pins the pipeline timeout so a hung provider call fails fast instead of on SDK defaults.
        var service = CreateService(OkHandler());

        Assert.Equal(TimeSpan.FromSeconds(120), service.NetworkTimeout);
    }
}
