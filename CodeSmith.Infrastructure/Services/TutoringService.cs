// == Tutoring Service Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Session-aware tutoring orchestration. Delegates problem generation to IProblemGenerator
/// and raw completions to ILlmServiceFactory.
/// </summary>
public class TutoringService : ITutoringService
{
    private readonly IProblemGenerator _problemGenerator;
    private readonly ILlmServiceFactory _factory;
    private readonly ISessionStore<ProblemSession> _sessionStore;
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILogger<TutoringService> _logger;

    private const int GuidanceMaxTokens = 1024; // Per-message guidance response budget

    public TutoringService(
        IProblemGenerator problemGenerator,
        ILlmServiceFactory factory,
        ISessionStore<ProblemSession> sessionStore,
        ICodeExecutionService codeExecutionService,
        ITutoringPromptTemplates templates,
        ILogger<TutoringService> logger)
    {
        _problemGenerator     = problemGenerator;
        _factory              = factory;
        _sessionStore         = sessionStore;
        _codeExecutionService = codeExecutionService;
        _templates            = templates;
        _logger               = logger;
    }

    // == Problem Generation == //

    public async Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default)
    {
        var (description, starterCode) = await _problemGenerator.GenerateAsync(difficulty, language, provider, ct);

        var session = new ProblemSession
        {
            Difficulty         = difficulty,
            Language           = language,
            Provider           = provider,
            ProblemDescription = description,
            StarterCode        = starterCode
        };

        _sessionStore.Set(session);
        _logger.LogInformation("Created session {SessionId} for {Difficulty} {Language}", session.SessionId, difficulty, language);
        return session;
    }

    // == Code Execution == //

    public async Task<CodeExecutionResult> RunCodeAsync(Guid sessionId, Language language, string code, CancellationToken ct = default)
    {
        var session = _sessionStore.Get(sessionId.ToString())
            ?? throw new SessionNotFoundException(sessionId);

        return await _codeExecutionService.ExecuteAsync(language, code, ct);
    }

    // == Guidance == //

    public async Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, GuidanceMode guidanceMode = GuidanceMode.Guidance, CancellationToken ct = default)
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

        var systemPrompt = _templates.GuidanceSystemPrompt(session.Language, session.ProblemDescription, session.StarterCode, editorContent, guidanceMode);
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
