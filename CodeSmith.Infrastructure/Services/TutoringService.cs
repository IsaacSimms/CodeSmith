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

    public Task<ProblemSession> GenerateProblemAsync(ProblemSpec spec, CancellationToken ct = default)
        => CreateSessionFromGenerationAsync(spec, () => _problemGenerator.GenerateAsync(spec, ct));

    public Task<ProblemSession> StreamGenerateProblemAsync(
        ProblemSpec spec,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default)
        => CreateSessionFromGenerationAsync(spec,
            () => _problemGenerator.StreamGenerateAsync(spec, onDescriptionDelta, onReset, ct));

    private async Task<ProblemSession> CreateSessionFromGenerationAsync(
        ProblemSpec spec,
        Func<Task<GeneratedProblem>> generate)
    {
        var generated = await generate();

        var session = new ProblemSession
        {
            Difficulty         = spec.Difficulty,
            Language           = spec.Language,
            Provider           = spec.Provider,
            Focus              = generated.Focus,   // Post-roll values, so a Random request still records what was asked for
            Topic              = generated.Topic,
            ProblemDescription = generated.Description,
            StarterCode        = generated.StarterCode
        };

        _sessionStore.Set(session);
        _logger.LogInformation(
            "Created session {SessionId} for {Difficulty} {Language} — focus '{Focus}', topic '{Topic}'",
            session.SessionId, spec.Difficulty, spec.Language, session.Focus, session.Topic);
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

            var systemPrompt = _templates.GuidanceSystemPrompt(session.Language, session.ProblemDescription, session.StarterCode, editorContent, guidanceMode, session.Focus);
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
