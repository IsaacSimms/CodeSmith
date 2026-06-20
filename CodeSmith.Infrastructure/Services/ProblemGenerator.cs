// == Problem Generator Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

public class ProblemGenerator : IProblemGenerator
{
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILlmServiceFactory _factory;
    private readonly IProblemResponseParser _parser;
    private readonly ILogger<ProblemGenerator> _logger;

    private const int MaxTokens  = 2000; // Enough for a full problem description + starter code
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

    // == GenerateAsync == //

    public async Task<(string Description, string StarterCode)> GenerateAsync(
        Difficulty difficulty, Language language, AiProvider provider, CancellationToken ct = default)
    {
        var request = _templates.ProblemGeneration(difficulty, language);
        _logger.LogInformation(
            "Generating {Difficulty} {Language} problem via {Provider} — category '{Category}', angle '{Angle}'",
            difficulty, request.LanguageLabel, provider, request.Category, request.Angle);

        var lastWasTruncated = false;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var userMessage = lastWasTruncated
                ? $"{request.UserMessage} Note: A previous attempt was cut off due to token limits. Please generate a complete problem."
                : request.UserMessage;

            var llmResponse = await _factory.Get(provider).CompleteAsync(
                CompletionRequest.SingleTurn(request.SystemPrompt, userMessage, ModelTier.Accurate, MaxTokens, "Tutoring:ProblemGeneration"), ct);

            lastWasTruncated = llmResponse.WasTruncated;

            if (llmResponse.WasTruncated)
            {
                _logger.LogWarning(
                    "Problem generation hit token limit on attempt {Attempt}/{Max}",
                    attempt + 1, MaxRetries + 1);
                continue;
            }

            var (description, starterCode) = _parser.Parse(llmResponse.Content);

            if (!string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(starterCode))
                return (description, starterCode);

            _logger.LogWarning(
                "Problem generation produced incomplete output on attempt {Attempt}/{Max} — description={Desc} chars, code={Code} chars",
                attempt + 1, MaxRetries + 1, description.Length, starterCode.Length);
        }

        _logger.LogError("Problem generation failed after {Max} attempts", MaxRetries + 1);
        throw new AiServiceException("Failed to generate a complete coding problem after multiple attempts. The response was malformed. Please try again.");
    }
}
