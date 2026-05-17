// == Problem Generator Implementation == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services;

public class ProblemGenerator : IProblemGenerator
{
    private readonly ITutoringPromptTemplates _templates;
    private readonly ILlmServiceFactory _factory;
    private readonly IProblemResponseParser _parser;
    private readonly ILogger<ProblemGenerator> _logger;

    private const int MaxTokens      = 2000; // Enough for a full problem description + starter code
    private const int MaxParseRetries = 2;   // Distinct from provider-level truncation retry

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

        for (var attempt = 0; attempt <= MaxParseRetries; attempt++)
        {
            var llmResponse = await _factory.GetLlmService<ITutoringLlmService>(provider)
                .GenerateProblemAsync(request.SystemPrompt, request.UserMessage, MaxTokens, ct);

            var (description, starterCode) = _parser.Parse(llmResponse.Content);

            if (!string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(starterCode))
                return (description, starterCode);

            _logger.LogWarning(
                "Problem generation produced incomplete output on attempt {Attempt}/{Max} — description={Desc} chars, code={Code} chars",
                attempt + 1, MaxParseRetries + 1, description.Length, starterCode.Length);
        }

        _logger.LogError("Problem generation produced malformed output after {Max} attempts", MaxParseRetries + 1);
        throw new AiServiceException("Failed to generate a complete coding problem after multiple attempts. The response was malformed. Please try again.");
    }
}
