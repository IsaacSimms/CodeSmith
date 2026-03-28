// == API Client Tests == //
using System.Net;
using System.Text.Json;
using CodeSmith.CLI.Services;
using CodeSmith.Core.Enums;

namespace CodeSmith.Tests.CLI;

public class ApiClientTests
{
    // == Mock HTTP Handler == //

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, object? responseBody = null)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseBody);
        return new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7111") };
    }

    [Fact]
    public async Task CreateSessionAsync_WithSuccessResponse_DeserializesSession()
    {
        var responseObj = new
        {
            sessionId = Guid.NewGuid(),
            difficulty = "Easy",
            problemDescription = "Test problem",
            starterCode = "public class Solution { }",
            messages = Array.Empty<object>(),
            createdAt = DateTime.UtcNow
        };

        var client = new ApiClient(CreateMockHttpClient(HttpStatusCode.OK, responseObj));

        var session = await client.CreateSessionAsync(Difficulty.Easy);

        Assert.Equal("Test problem", session.ProblemDescription);
        Assert.Equal("public class Solution { }", session.StarterCode);
    }

    [Fact]
    public async Task CreateSessionAsync_WithErrorResponse_ThrowsHttpRequestException()
    {
        var client = new ApiClient(CreateMockHttpClient(
            HttpStatusCode.BadRequest,
            new { error = "Invalid difficulty" }));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.CreateSessionAsync(Difficulty.Easy));
    }

    [Fact]
    public async Task SendChatAsync_WithSuccessResponse_ReturnsResponseText()
    {
        var responseObj = new { response = "Here's a hint for you." };
        var client = new ApiClient(CreateMockHttpClient(HttpStatusCode.OK, responseObj));

        var response = await client.SendChatAsync(Guid.NewGuid(), "help me");

        Assert.Equal("Here's a hint for you.", response);
    }

    [Fact]
    public async Task SendChatAsync_WithNotFound_ThrowsHttpRequestException()
    {
        var client = new ApiClient(CreateMockHttpClient(
            HttpStatusCode.NotFound,
            new { error = "Session not found" }));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SendChatAsync(Guid.NewGuid(), "help"));
    }

    // == Mock Handler Implementation == //

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;

        public MockHttpMessageHandler(HttpStatusCode statusCode, object? responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_responseBody != null)
            {
                response.Content = new StringContent(
                    JsonSerializer.Serialize(_responseBody),
                    System.Text.Encoding.UTF8,
                    "application/json");
            }
            return Task.FromResult(response);
        }
    }
}
