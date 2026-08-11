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

    private const int GuidanceMaxTokens = 1024; // Per-message guidance response budget

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

    // The turn mechanics (per-session lock, load-or-throw, streaming dispatch, persist, rollback) live
    // behind IGuidanceConversation — this surface supplies only its prompt data and projects the result.
    private async Task<ChatResponse> ExecuteGuidanceAsync(
        Guid sessionId, string userMessage, string? editorContent, GuidanceMode guidanceMode,
        Func<string, CancellationToken, Task>? onDelta, CancellationToken ct)
    {
        var llmResponse = await _guidance.RunTurnAsync(_sessionStore, sessionId, session => new GuidanceTurnRequest
        {
            SystemPrompt = _templates.GuidanceSystemPrompt(session.Language, session.ProblemDescription, session.StarterCode, editorContent, guidanceMode, session.Focus),
            UserMessage  = userMessage,
            MaxTokens    = GuidanceMaxTokens,
            Feature      = "Tutoring:Guidance"
        }, onDelta, ct);

        return new ChatResponse
        {
            Response          = llmResponse.Content,
            ContextTokensUsed = llmResponse.InputTokensUsed,
            ContextWindowSize = llmResponse.ContextWindowSize
        };
    }
}
