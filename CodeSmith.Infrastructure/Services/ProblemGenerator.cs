// == Problem Generator Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using CodeSmith.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

public class ProblemGenerator : IProblemGenerator
{
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILlmServiceFactory _factory;
    private readonly IProblemResponseParser _parser;
    private readonly ILogger<ProblemGenerator> _logger;

    private const int MaxTokens  = 4000; // Headroom so truncation retries rarely fire — each retry is a full extra Accurate-tier completion; reserve holds against this but settle refunds to actuals
    private const int MaxRetries = 2;    // Shared budget across truncation and parse failures

    public ProblemGenerator(
        ITutoringPromptTemplates  templates,
        ILlmServiceFactory        factory,
        IProblemResponseParser    parser,
        ILogger<ProblemGenerator> logger)
    {
        _templates = templates;
        _factory   = factory;
        _parser    = parser;
        _logger    = logger;
    }

    // == GenerateAsync / StreamGenerateAsync == //

    public Task<GeneratedProblem> GenerateAsync(ProblemSpec spec, CancellationToken ct = default)
        => ExecuteGenerationAsync(spec,
            (llm, completion, token) => llm.CompleteAsync(completion, token), onReset: null, ct);

    public Task<GeneratedProblem> StreamGenerateAsync(
        ProblemSpec spec,
        Func<string, CancellationToken, Task> onDescriptionDelta,
        Func<CancellationToken, Task> onReset,
        CancellationToken ct = default)
        => ExecuteGenerationAsync(spec,
            // A fresh filter per attempt: each retry re-streams its description from a clean scanner state
            (llm, completion, token) => llm.StreamAsync(completion, new DescriptionStreamFilter(onDescriptionDelta).FeedAsync, token),
            onReset, ct);

    // == Generation Attempt Loop Core == //

    // One implementation of the attempt loop (truncation + parse-failure retries with a shared
    // budget) for both operation shapes; onReset fires before each retry so streaming consumers can
    // clear text an abandoned attempt already showed.
    private async Task<GeneratedProblem> ExecuteGenerationAsync(
        ProblemSpec spec,
        Func<ILlmService, CompletionRequest, CancellationToken, Task<LlmResponse>> invoke,
        Func<CancellationToken, Task>? onReset,
        CancellationToken ct)
    {
        // Resolved once, outside the attempt loop — retries must re-ask for the same focus and topic
        var request = _templates.ProblemGeneration(spec);
        _logger.LogInformation(
            "Generating {Difficulty} {Language} problem via {Provider} — focus '{Focus}', topic '{Topic}'",
            spec.Difficulty, request.LanguageLabel, spec.Provider, request.Focus, request.Topic);

        var lastWasTruncated = false;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0 && onReset is not null)
                await onReset(ct);

            // One span per attempt so silent retries (each a full extra completion) show up in traces
            using var attemptSpan = CodeSmithDiagnostics.Source.StartActivity("problem.generation.attempt");
            attemptSpan?.SetTag("codesmith.attempt", attempt + 1);

            var userMessage = lastWasTruncated
                ? $"{request.UserMessage} Note: A previous attempt was cut off due to token limits. Please generate a complete problem."
                : request.UserMessage;

            var llmResponse = await invoke(_factory.Get(spec.Provider),
                CompletionRequest.SingleTurn(request.SystemPrompt, userMessage, ModelTier.Accurate, MaxTokens, "Tutoring:ProblemGeneration"), ct);

            lastWasTruncated = llmResponse.WasTruncated;
            attemptSpan?.SetTag("codesmith.truncated", llmResponse.WasTruncated);

            if (llmResponse.WasTruncated)
            {
                _logger.LogWarning(
                    "Problem generation hit token limit on attempt {Attempt}/{Max}",
                    attempt + 1, MaxRetries + 1);
                continue;
            }

            var (description, starterCode) = _parser.Parse(llmResponse.Content);

            var parseComplete = !string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(starterCode);
            attemptSpan?.SetTag("codesmith.parse_complete", parseComplete);

            if (parseComplete)
                return new GeneratedProblem(description, starterCode, request.Focus, request.Topic);

            _logger.LogWarning(
                "Problem generation produced incomplete output on attempt {Attempt}/{Max} — description={Desc} chars, code={Code} chars",
                attempt + 1, MaxRetries + 1, description.Length, starterCode.Length);
        }

        _logger.LogError("Problem generation failed after {Max} attempts", MaxRetries + 1);
        throw new AiServiceException("Failed to generate a complete coding problem after multiple attempts. The response was malformed. Please try again.");
    }

    // == Description Stream Filter == //

    /// <summary>
    /// Stateful scanner that turns raw completion deltas into description-only deltas for the
    /// DESCRIPTION / STARTER_CODE format. Nothing is emitted until the DESCRIPTION marker completes;
    /// once the STARTER_CODE marker begins, emission stops for good. Markers may split across any
    /// delta boundary, so the scanner holds back the longest pending suffix that could still be
    /// completing a marker and releases it only once it provably is not one.
    /// </summary>
    private sealed class DescriptionStreamFilter
    {
        private const string DescriptionMarker = "DESCRIPTION:";
        private const string StarterCodeMarker = "STARTER_CODE:";

        private readonly Func<string, CancellationToken, Task> _emit;
        private string _pending = string.Empty;
        private bool _inDescription;
        private bool _done;
        private bool _skippingLeadingWhitespace = true;

        public DescriptionStreamFilter(Func<string, CancellationToken, Task> emit) => _emit = emit;

        public async Task FeedAsync(string delta, CancellationToken ct)
        {
            if (_done) return;
            _pending += delta;

            if (!_inDescription)
            {
                var markerIndex = _pending.IndexOf(DescriptionMarker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) return;   // preamble before the marker is tiny; keep buffering
                _pending       = _pending[(markerIndex + DescriptionMarker.Length)..];
                _inDescription = true;
            }

            string emitText;
            var codeMarkerIndex = _pending.IndexOf(StarterCodeMarker, StringComparison.OrdinalIgnoreCase);
            if (codeMarkerIndex >= 0)
            {
                emitText = _pending[..codeMarkerIndex];
                _pending = string.Empty;
                _done    = true;
            }
            else
            {
                var held = LongestSuffixThatCouldStartMarker(_pending, StarterCodeMarker);
                emitText = _pending[..^held];
                _pending = _pending[(_pending.Length - held)..];
            }

            if (_skippingLeadingWhitespace)
            {
                emitText = emitText.TrimStart();
                if (emitText.Length > 0) _skippingLeadingWhitespace = false;
            }

            if (emitText.Length > 0)
                await _emit(emitText, ct);
        }

        // Length of the longest suffix of text that matches a proper prefix of marker (case-insensitive)
        private static int LongestSuffixThatCouldStartMarker(string text, string marker)
        {
            for (var length = Math.Min(text.Length, marker.Length - 1); length > 0; length--)
            {
                if (string.Compare(text, text.Length - length, marker, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
                    return length;
            }
            return 0;
        }
    }
}
