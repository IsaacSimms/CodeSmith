// == Anthropic Service Implementation == //
using Anthropic;
using Anthropic.Models.Messages;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="IAnthropicService"/> using the official Anthropic C# SDK.
/// Manages Claude API calls for problem generation and guided tutoring.
/// </summary>
public class AnthropicService : IAnthropicService
{
    private readonly AnthropicClient _client;
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<AnthropicService> _logger;

    private const string Model = "claude-sonnet-4-20250514";

    // == System Prompts == //
    private const string ProblemGenerationSystemPrompt =
        """
        You are an expert coding tutor who creates C# programming problems.
        When asked to generate a problem, respond with exactly two sections:

        DESCRIPTION:
        (Write a clear problem description here)

        STARTER_CODE:
        (Write a C# code stub/template here)

        Do not include solutions or hints. The starter code should compile but be incomplete.
        """;

    private const string GuidanceSystemPromptTemplate =
        """
        You are an expert coding tutor helping a student solve a C# programming problem.
        Guide the student toward the solution without giving away the answer directly.
        Ask leading questions, point out relevant concepts, and help them think through the problem.
        If they are stuck, give small hints rather than full solutions.

        The problem they are working on:
        {0}

        The starter code provided:
        {1}
        """;

    public AnthropicService(
        IOptions<AnthropicOptions> options,
        ISessionStore sessionStore,
        ILogger<AnthropicService> logger)
    {
        _client = new AnthropicClient { ApiKey = options.Value.ApiKey };
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating {Difficulty} problem", difficulty);

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 2048,
                System = ProblemGenerationSystemPrompt,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"Generate a {difficulty} difficulty C# coding problem."
                    }
                ]
            }, ct);

            var responseText = ExtractTextContent(response);
            var (description, starterCode) = ParseProblemResponse(responseText);

            var session = new ProblemSession
            {
                Difficulty = difficulty,
                ProblemDescription = description,
                StarterCode = starterCode
            };

            _sessionStore.Set(session);

            _logger.LogInformation("Created session {SessionId} with {Difficulty} problem", session.SessionId, difficulty);
            return session;
        }
        catch (Exception ex) when (ex is not AnthropicApiException)
        {
            _logger.LogError(ex, "Failed to generate problem from Anthropic API");
            throw new AnthropicApiException("Failed to generate coding problem. Please try again.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> GetGuidanceAsync(Guid sessionId, string userMessage, CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId)
            ?? throw new SessionNotFoundException(sessionId);

        _logger.LogInformation("Processing guidance request for session {SessionId}", sessionId);

        try
        {
            // Add user message to history
            session.Messages.Add(new Core.Models.ChatMessage
            {
                Role = MessageRole.User,
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            });

            // Build conversation history for Claude
            var messages = session.Messages.Select(m => new MessageParam
            {
                Role = m.Role == MessageRole.User ? Role.User : Role.Assistant,
                Content = m.Content
            }).ToList();

            var systemPrompt = string.Format(
                GuidanceSystemPromptTemplate,
                session.ProblemDescription,
                session.StarterCode);

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 1024,
                System = systemPrompt,
                Messages = messages
            }, ct);

            var responseText = ExtractTextContent(response);

            // Add assistant response to history
            session.Messages.Add(new Core.Models.ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = responseText,
                Timestamp = DateTime.UtcNow
            });

            _sessionStore.Set(session);

            return responseText;
        }
        catch (Exception ex) when (ex is not AnthropicApiException and not SessionNotFoundException)
        {
            _logger.LogError(ex, "Failed to get guidance from Anthropic API for session {SessionId}", sessionId);
            throw new AnthropicApiException("Failed to get guidance. Please try again.", ex);
        }
    }

    // == Response Parsing Helpers == //

    /// <summary>
    /// Extracts text content from an Anthropic message response.
    /// </summary>
    internal static string ExtractTextContent(Message response)
    {
        var texts = new List<string>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                texts.Add(textBlock.Text);
            }
        }
        return string.Join("", texts);
    }

    /// <summary>
    /// Parses the structured problem response into description and starter code.
    /// </summary>
    internal static (string Description, string StarterCode) ParseProblemResponse(string responseText)
    {
        var description = string.Empty;
        var starterCode = string.Empty;

        var descIndex = responseText.IndexOf("DESCRIPTION:", StringComparison.OrdinalIgnoreCase);
        var codeIndex = responseText.IndexOf("STARTER_CODE:", StringComparison.OrdinalIgnoreCase);

        if (descIndex >= 0 && codeIndex >= 0)
        {
            description = responseText[(descIndex + "DESCRIPTION:".Length)..codeIndex].Trim();
            starterCode = responseText[(codeIndex + "STARTER_CODE:".Length)..].Trim();
        }
        else
        {
            // Fallback: treat entire response as description
            description = responseText.Trim();
        }

        return (description, starterCode);
    }
}
