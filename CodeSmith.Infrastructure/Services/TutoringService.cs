// == Tutoring Service Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Session-aware tutoring orchestration. Delegates prompt construction to ITutoringPromptTemplates
/// and raw completions to ILlmServiceFactory.
/// </summary>
public class TutoringService : ITutoringService
{
    private readonly ILlmServiceFactory _factory;
    private readonly ISessionStore<ProblemSession> _sessionStore;
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILogger<TutoringService> _logger;

    private const int ProblemMaxTokens  = 2000;  // Enough for a full problem description + starter code
    private const int GuidanceMaxTokens = 1024;  // Per-message guidance response budget

    public TutoringService(
        ILlmServiceFactory factory,
        ISessionStore<ProblemSession> sessionStore,
        ICodeExecutionService codeExecutionService,
        ITutoringPromptTemplates templates,
        ILogger<TutoringService> logger)
    {
        _factory              = factory;
        _sessionStore         = sessionStore;
        _codeExecutionService = codeExecutionService;
        _templates            = templates;
        _logger               = logger;
    }

    // == Problem Generation == //

    public async Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default)
    {
        var request = _templates.ProblemGeneration(difficulty, language);
        _logger.LogInformation("Generating {Difficulty} {Language} problem via {Provider}", difficulty, request.LanguageLabel, provider);
        _logger.LogInformation("Category '{Category}', angle '{Angle}'", request.Category, request.Angle);

        // Retry up to 2 times if the parsed output is incomplete (malformed response, not truncation — truncation is handled by the provider)
        const int maxParseRetries = 2;
        for (var attempt = 0; attempt <= maxParseRetries; attempt++)
        {
            var llmResponse = await _factory.GetLlmService<ITutoringLlmService>(provider).GenerateProblemAsync(request.SystemPrompt, request.UserMessage, ProblemMaxTokens, ct);
            var (description, starterCode) = _templates.ParseProblemResponse(llmResponse.Content);

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
                _logger.LogInformation("Created session {SessionId} for {Difficulty} {Language}", session.SessionId, difficulty, request.LanguageLabel);
                return session;
            }

            _logger.LogWarning("Problem generation produced incomplete output on attempt {Attempt}/{Max} — description={Desc} chars, code={Code} chars",
                attempt + 1, maxParseRetries + 1, description.Length, starterCode.Length);
        }

        _logger.LogError("Problem generation produced malformed output after {Max} attempts", maxParseRetries + 1);
        throw new AiServiceException("Failed to generate a complete coding problem after multiple attempts. The response was malformed. Please try again.");
    }

    // == Code Execution == //

    public async Task<CodeExecutionResult> RunCodeAsync(Guid sessionId, Language language, string code, CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId.ToString())
            ?? throw new SessionNotFoundException(sessionId);

        return await _codeExecutionService.ExecuteAsync(language, code, ct);
    }

    // == Guidance == //

    public async Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, bool isCodeAnalysis = false, CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId.ToString())
            ?? throw new SessionNotFoundException(sessionId);

        _logger.LogInformation("Processing guidance request for session {SessionId}", sessionId);

        // Add user message to history before calling the LLM
        session.Messages.Add(new ChatMessage
        {
            Role      = MessageRole.User,
            Content   = userMessage,
            Timestamp = DateTime.UtcNow
        });

        var systemPrompt = _templates.GuidanceSystemPrompt(session.Language, session.ProblemDescription, session.StarterCode, editorContent, isCodeAnalysis);
        var llmResponse  = await _factory.GetLlmService<ITutoringLlmService>(session.Provider).GetGuidanceAsync(systemPrompt, session.Messages, GuidanceMaxTokens, ct);

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
}
