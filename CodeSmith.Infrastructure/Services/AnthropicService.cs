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
    private const string ProblemGenerationSystemPromptTemplate =
        """
        You are an expert coding tutor who creates {0} programming problems.
        When asked to generate a problem, respond with exactly two sections:

        DESCRIPTION:
        (Write a clear problem description here)

        STARTER_CODE:
        (Write a {0} code stub/template here using idiomatic syntax for the language)

        Do not include solutions or hints. The starter code should compile but be incomplete. You could choose for the prompt to have
        a bug or bugs the user needs to solve. It could also be complete, but the prompt could be for a user to add a new feature or block of code for a specific functionailty.
        Only output the required code in the STARTER_CODE section. Do not output ''' or any other formatting.
        Starter code can also involve a class for unit testing, if applicable.
        """;

    private const string GuidanceSystemPromptTemplate =
        """
        You are an expert coding tutor helping a student solve a {0} programming problem.
        Guide the student toward the solution without giving away the answer directly.
        Ask leading questions, point out relevant concepts, and help them think through the problem.
        If they are stuck, give small hints rather than full solutions.
        Use {0} syntax and idioms in any code examples or snippets you provide.

        The problem they are working on:
        {1}

        The starter code provided:
        {2}
        """;

    private const string EditorContentSection =
        """


        The student's current code in the editor:
        {0}
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

    public async Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, CancellationToken ct = default)
    {
        var languageLabel = GetLanguageLabel(language);
        _logger.LogInformation("Generating {Difficulty} {Language} problem", difficulty, languageLabel);

        try
        {
            var systemPrompt = string.Format(ProblemGenerationSystemPromptTemplate, languageLabel);

            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 2048,
                System = systemPrompt,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = $"Generate a {difficulty} difficulty {languageLabel} coding problem."
                    }
                ]
            }, ct);

            var responseText = ExtractTextContent(response);
            var (description, starterCode) = ParseProblemResponse(responseText);

            var session = new ProblemSession
            {
                Difficulty = difficulty,
                Language = language,
                ProblemDescription = description,
                StarterCode = starterCode
            };

            _sessionStore.Set(session);

            _logger.LogInformation("Created session {SessionId} with {Difficulty} {Language} problem", session.SessionId, difficulty, languageLabel);
            return session;
        }
        catch (Exception ex) when (ex is not AnthropicApiException)
        {
            _logger.LogError(ex, "Failed to generate problem from Anthropic API");
            throw new AnthropicApiException("Failed to generate coding problem. Please try again.", ex);
        }
    }

    public async Task<string> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, CancellationToken ct = default)
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
                GetLanguageLabel(session.Language),
                session.ProblemDescription,
                session.StarterCode);

            // Append the student's current editor content when available
            if (!string.IsNullOrWhiteSpace(editorContent))
                systemPrompt += string.Format(EditorContentSection, editorContent);

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

    // == Language Helpers == //

    internal static string GetLanguageLabel(Language language) => language switch  // Maps the Language enum to the human-readable label used in prompts and user messages
    {
        Language.CSharp => "C#",
        Language.Cpp    => "C++",
        Language.Go     => "Go",
        Language.Rust   => "Rust",
        Language.Python => "Python",
        Language.Java   => "Java",
        Language.TypeScript => "TypeScript",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language")
    };

    // == Response Parsing Helpers == //

    internal static string ExtractTextContent(Message response)  // Extracts text content from an Anthropic message response
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

    internal static (string Description, string StarterCode) ParseProblemResponse(string responseText)  // Parses the structured problem response into description and starter code
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
