// == Tutoring Service Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="ITutoringService"/>. Owns prompt templates, session lifecycle,
/// and conversation history. Delegates raw completions to the injected <see cref="ILlmServiceFactory"/>.
/// </summary>
public class TutoringService : ITutoringService
{
    private readonly ILlmServiceFactory _factory;
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<TutoringService> _logger;

    private const int ProblemMaxTokens  = 2000;  // Enough for a full problem description + starter code
    private const int GuidanceMaxTokens = 1024;  // Per-message guidance response budget

    // == Problem Variety Data == //
    // "Standard implementation" appears 3× for roughly 30% baseline probability; other entries add creative variety
    internal static readonly string[] ProblemCategories =
    [
        "arrays and strings",
        "hash maps and sets",
        "trees and graphs",
        "dynamic programming",
        "object-oriented design",
        "functional patterns and recursion",
        "real-world simulation",
        "math and number theory",
        "state machines",
        "parsing and string processing",
        "bit manipulation",
        "sorting and searching",
    ];

    internal static readonly string[] ProblemAngles =
    [
        "Standard implementation",
        "Standard implementation",
        "Standard implementation",
        "Bug fix — the starter code contains one or more subtle bugs the student must find and fix",
        "Performance optimization — a naive solution is provided; the student must improve its time or space complexity",
        "Feature extension — working code exists but lacks a specific feature the student must add",
        "Unusual constraints — solve with a restriction such as no built-in library methods, single pass, or O(1) extra space",
        "Edge-case gauntlet — design tests that specifically stress boundary conditions and non-obvious inputs",
        "Real-world domain — frame the problem inside an interesting context (e.g., a game loop, compiler pass, OS scheduler, library catalog, financial ledger)",
        "Refactoring — code that works but is poorly structured; the student must improve it without changing behavior",
    ];

    // == System Prompts == //
    private const string ProblemGenerationSystemPromptTemplate =
        """
        You are an expert coding tutor who creates {0} programming problems.
        You will receive a topic area and an approach style in the user message — honor them faithfully when generating the problem.

        Think creatively about framing. Do not default to "write a function that does X" every time. When the approach calls for it,
        embed the problem in a richer real-world context: a game engine, a text parser, an inventory system, a mini-compiler, a
        task scheduler, a financial ledger, etc. The scenario should feel plausible and interesting to a developer.

        For test cases in the starter code: include a mix of typical inputs, edge cases, and non-obvious boundary conditions.
        Do not only test the happy path — surprising or tricky inputs make the exercise more educational.

        When asked to generate a problem, respond with exactly two sections:

        DESCRIPTION:
        (Write a clear problem description here)

        STARTER_CODE:
        (Write a {0} code stub/template here using idiomatic syntax for the language)

        Do not include solutions or hints. The starter code should compile but be incomplete. Depending on the approach style,
        it may contain a subtle bug to fix, a naive implementation to optimize, a partial feature to extend, or a working but
        messy structure to refactor. Only output the required code in the STARTER_CODE section. Do not output ''' or any other formatting.
        There is a code execution button in the solution, when pressed, it executes the current code and displays results to a terminal.
        When outputting the STARTER_CODE, keep in mind that the user will be able to run it as-is, so it should be a valid code snippet
        that compiles and runs without errors. Add multiple test cases in the starter code that the user can run to verify their solution.
        The tests should be clearly labeled and cover a range of inputs including edge cases.
        The user will be able to modify the code and re-run the tests, so they should be designed to help the user validate their solution as they work on it.
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

    private const string CodeAnalysisSystemPromptTemplate =
        """
        You are an expert coding tutor helping a student analyze the results of running their {0} code.
        The student has just executed their solution and shared the output with you.
        Interpret the execution results clearly: explain what the output means, whether the tests passed or failed,
        and what the errors or unexpected values indicate — without revealing the fix directly.
        Ask a leading question or give a small nudge to help the student figure out what to change next.
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

    public TutoringService(
        ILlmServiceFactory factory,
        ISessionStore sessionStore,
        ILogger<TutoringService> logger)
    {
        _factory      = factory;
        _sessionStore = sessionStore;
        _logger       = logger;
    }

    // == Problem Generation == //

    public async Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default)
    {
        var languageLabel = GetLanguageLabel(language);
        _logger.LogInformation("Generating {Difficulty} {Language} problem via {Provider}", difficulty, languageLabel, provider);

        var category = ProblemCategories[Random.Shared.Next(ProblemCategories.Length)];
        var angle    = ProblemAngles[Random.Shared.Next(ProblemAngles.Length)];

        _logger.LogInformation("Category '{Category}', angle '{Angle}'", category, angle);

        var systemPrompt = string.Format(ProblemGenerationSystemPromptTemplate, languageLabel);
        var userMessage  = $"Generate a {difficulty} difficulty {languageLabel} coding problem. Topic area: {category}. Approach: {angle}.";

        // Retry up to 2 times if the parsed output is incomplete (malformed response, not truncation — truncation is handled by the provider)
        const int maxParseRetries = 2;
        for (var attempt = 0; attempt <= maxParseRetries; attempt++)
        {
            var llmResponse = await _factory.GetService(provider).GenerateProblemAsync(systemPrompt, userMessage, ProblemMaxTokens, ct);
            var (description, starterCode) = ParseProblemResponse(llmResponse.Content);

            if (!string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(starterCode))
            {
                var session = new ProblemSession
                {
                    Difficulty         = difficulty,
                    Language           = language,
                    Provider           = provider,
                    ProblemDescription = description,
                    StarterCode        = starterCode
                };

                _sessionStore.Set(session);
                _logger.LogInformation("Created session {SessionId} for {Difficulty} {Language}", session.SessionId, difficulty, languageLabel);
                return session;
            }

            _logger.LogWarning("Problem generation produced incomplete output on attempt {Attempt}/{Max} — description={Desc} chars, code={Code} chars",
                attempt + 1, maxParseRetries + 1, description.Length, starterCode.Length);
        }

        _logger.LogError("Problem generation produced malformed output after {Max} attempts", maxParseRetries + 1);
        throw new AiServiceException("Failed to generate a complete coding problem after multiple attempts. The response was malformed. Please try again.");
    }

    // == Guidance == //

    public async Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, bool isCodeAnalysis = false, CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId)
            ?? throw new SessionNotFoundException(sessionId);

        _logger.LogInformation("Processing guidance request for session {SessionId}", sessionId);

        // Add user message to history before calling the LLM
        session.Messages.Add(new ChatMessage
        {
            Role      = MessageRole.User,
            Content   = userMessage,
            Timestamp = DateTime.UtcNow
        });

        var promptTemplate = isCodeAnalysis ? CodeAnalysisSystemPromptTemplate : GuidanceSystemPromptTemplate;
        var systemPrompt   = string.Format(promptTemplate, GetLanguageLabel(session.Language), session.ProblemDescription, session.StarterCode);

        // Append the student's current editor content when available
        if (!string.IsNullOrWhiteSpace(editorContent))
            systemPrompt += string.Format(EditorContentSection, editorContent);

        var llmResponse = await _factory.GetService(session.Provider).GetGuidanceAsync(systemPrompt, session.Messages, GuidanceMaxTokens, ct);

        // Add assistant response to history
        session.Messages.Add(new ChatMessage
        {
            Role      = MessageRole.Assistant,
            Content   = llmResponse.Content,
            Timestamp = DateTime.UtcNow
        });

        _sessionStore.Set(session);

        return new ChatResponse
        {
            Response          = llmResponse.Content,
            ContextTokensUsed = llmResponse.InputTokensUsed,
            ContextWindowSize = llmResponse.ContextWindowSize
        };
    }

    // == Language Helpers == //

    internal static string GetLanguageLabel(Language language) => language switch  // Maps the Language enum to the human-readable label used in prompts
    {
        Language.CSharp      => "C#",
        Language.Cpp         => "C++",
        Language.Go          => "Go",
        Language.Rust        => "Rust",
        Language.Python      => "Python",
        Language.Java        => "Java",
        Language.TypeScript  => "TypeScript",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unknown language")
    };

    // == Response Parsing == //

    internal static (string Description, string StarterCode) ParseProblemResponse(string responseText)  // Parses the structured DESCRIPTION / STARTER_CODE response into its two sections
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
