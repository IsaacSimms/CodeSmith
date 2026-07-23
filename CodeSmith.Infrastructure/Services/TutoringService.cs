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
    private readonly IGuidanceConversation _guidance;
    private readonly ISessionStore<ProblemSession> _sessionStore;
    private readonly ICodeExecutionService _codeExecutionService;
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILogger<TutoringService> _logger;

    private const int GuidanceMaxTokens    = 1024; // Per-message guidance response budget
    private const int GuidanceHistoryWindow = 20;  // Max messages retained before older turns are trimmed

    public TutoringService(
        IProblemGenerator problemGenerator,
        IGuidanceConversation guidance,
        ISessionStore<ProblemSession> sessionStore,
        ICodeExecutionService codeExecutionService,
        ITutoringPromptTemplates templates,
        ILogger<TutoringService> logger)
    {
        _problemGenerator     = problemGenerator;
        _guidance             = guidance;
        _sessionStore         = sessionStore;
        _codeExecutionService = codeExecutionService;
        _templates            = templates;
        _logger               = logger;
    }

    // == Problem Generation == //

    public Task<ProblemSession> GenerateProblemAsync(Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default)
        => CreateSessionFromGenerationAsync(difficulty, language, provider,
            () => _problemGenerator.GenerateAsync(difficulty, language, provider, ct));

    public Task<ProblemSession> StreamGenerateProblemAsync(
        Difficulty difficulty, Language language, AiProvider provider,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default)
        => CreateSessionFromGenerationAsync(difficulty, language, provider,
            () => _problemGenerator.StreamGenerateAsync(difficulty, language, provider, onDescriptionDelta, onReset, ct));

    private async Task<ProblemSession> CreateSessionFromGenerationAsync(
        Difficulty difficulty, Language language, AiProvider provider,
        Func<Task<(string Description, string StarterCode)>> generate)
    {
        var (description, starterCode) = await generate();

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

        return await _codeExecutionService.ExecuteAsync(
            new CodeExecutionRequest
            {
                Language = language,
                Code = code,
                SessionId = sessionId
            },
            ct);
    }

    // == Guidance == //

    public Task<ChatResponse> GetGuidanceAsync(Guid sessionId, string userMessage, string? editorContent = null, GuidanceMode guidanceMode = GuidanceMode.Guidance, CancellationToken ct = default)
        => ExecuteGuidanceAsync(sessionId, userMessage, editorContent, guidanceMode, onDelta: null, ct);

    public Task<ChatResponse> StreamGuidanceAsync(
        Guid sessionId, string userMessage, string? editorContent, GuidanceMode guidanceMode,
        Func<string, CancellationToken, Task> onDelta,
        CancellationToken ct = default)
        => ExecuteGuidanceAsync(sessionId, userMessage, editorContent, guidanceMode, onDelta, ct);

    // A streaming turn holds the same per-session lock for its whole duration as a blocking one —
    // partial turns are never persisted, so nothing else may interleave while the stream is open.
    private async Task<ChatResponse> ExecuteGuidanceAsync(
        Guid sessionId, string userMessage, string? editorContent, GuidanceMode guidanceMode,
        Func<string, CancellationToken, Task>? onDelta, CancellationToken ct)
    {
        // Serialize per session: a Guidance Turn mutates the shared Messages list, so concurrent turns
        // on the same session must not interleave (which would corrupt the user/assistant alternation).
        return await _sessionStore.WithSessionLockAsync(sessionId.ToString(), async () =>
        {
            var session = _sessionStore.Get(sessionId.ToString())
                ?? throw new SessionNotFoundException(sessionId);

            _logger.LogInformation("Processing guidance request for session {SessionId}", sessionId);

            var systemPrompt = _templates.GuidanceSystemPrompt(session.Language, session.ProblemDescription, session.StarterCode, editorContent, guidanceMode);
            var turnRequest  = new GuidanceTurnRequest
            {
                SystemPrompt = systemPrompt,
                UserMessage  = userMessage,
                MaxTokens    = GuidanceMaxTokens,
                MaxTurns     = GuidanceHistoryWindow,
                Feature      = "Tutoring:Guidance"
            };

            var llmResponse = onDelta is null
                ? await _guidance.RunTurnAsync(session.Provider, session.Messages, turnRequest, () => _sessionStore.Set(session), ct)
                : await _guidance.StreamTurnAsync(session.Provider, session.Messages, turnRequest, onDelta, () => _sessionStore.Set(session), ct);

            return new ChatResponse
            {
                Response          = llmResponse.Content,
                ContextTokensUsed = llmResponse.InputTokensUsed,
                ContextWindowSize = llmResponse.ContextWindowSize
            };
        }, ct);
    }
}
