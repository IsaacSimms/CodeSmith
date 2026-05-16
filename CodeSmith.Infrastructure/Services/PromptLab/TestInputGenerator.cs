// == Test Input Generation Phase == //
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.PromptLab;
using Microsoft.Extensions.Logging;

namespace CodeSmith.Infrastructure.Services.PromptLab;

public interface ITestInputGenerator
{
    Task<List<TestInput>> GenerateAsync(Challenge challenge, AiProvider provider, CancellationToken ct);
}

public sealed class TestInputGenerator : ITestInputGenerator
{
    private readonly ILlmServiceFactory _factory;
    private readonly ILogger<TestInputGenerator> _logger;

    private const int GenerationMaxTokens = 600;

    public TestInputGenerator(ILlmServiceFactory factory, ILogger<TestInputGenerator> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // == GenerateAsync == //

    public async Task<List<TestInput>> GenerateAsync(Challenge challenge, AiProvider provider, CancellationToken ct)
    {
        // Pre-decide input 3 and 4 types server-side for a true 50/50 split
        var input3Type = Random.Shared.Next(2) == 0 ? "standard" : "edge case";
        var input4Type = Random.Shared.Next(2) == 0 ? "standard" : "edge case";

        var examplesJson = JsonSerializer.Serialize(
            challenge.TestInputs.Select(t => new { t.Label, t.UserMessage, t.ExpectedBehavior }));

        var prompt = $"""
            Generate exactly 4 test inputs for this prompt engineering challenge.

            Challenge: {challenge.Title}
            Category: {challenge.Category}
            Description: {challenge.Description.Trim()}
            Locked System Prompt (context only): {challenge.LockedSystemPrompt}

            Reference inputs (illustrate the domain and style — do NOT copy them):
            {examplesJson}

            Generation rules:
            - Input gen-1: standard — a typical, representative case for this challenge
            - Input gen-2: standard — another typical case with different subject matter from gen-1
            - Input gen-3: {input3Type}{(input3Type == "edge case" ? " — surprising or interesting angle or subject matter, but equally solvable by the same prompt technique as standard inputs" : " — a typical case with different subject matter from gen-1 and gen-2")}
            - Input gen-4: {input4Type}{(input4Type == "edge case" ? " — surprising or interesting angle or subject matter, but equally solvable by the same prompt technique as standard inputs" : " — a typical case with different subject matter from the other inputs")}

            All inputs must have distinct subject matter from each other and from the reference inputs.
            Edge cases should be surprising or interesting, NOT harder to solve — the same prompt engineering technique must work equally well.

            Return ONLY a valid JSON array of exactly 4 objects. Each object must have exactly these string fields:
            "inputId" (gen-1 through gen-4), "label" (2-4 word description), "userMessage" (the message to send), "expectedBehavior" (what a correct response must do).
            No preamble, no markdown fences, no explanation — JSON array only.
            """;

        const string systemPrompt = "You generate test inputs for prompt engineering challenges. Return only a valid JSON array as specified — no preamble.";
        var response = await _factory.GetLlmService<IPromptLabLlmService>(provider)
            .GenerateTestInputsAsync(systemPrompt, prompt, GenerationMaxTokens, ct);

        var json  = ExtractJson(response.Content);
        var items = JsonSerializer.Deserialize<List<GeneratedTestInputDto>>(json)
            ?? throw new InvalidOperationException("Generation returned null JSON.");

        if (items.Count != 4)
            throw new InvalidOperationException($"Expected 4 generated inputs, got {items.Count}.");

        return items.Select((item, i) => new TestInput
        {
            InputId          = item.InputId ?? $"gen-{i + 1}",
            Label            = item.Label ?? "Unlabeled",
            UserMessage      = item.UserMessage ?? "",
            ExpectedBehavior = item.ExpectedBehavior ?? ""
        }).ToList();
    }

    // == Helpers == //

    private static string ExtractJson(string text)  // Strips markdown code fences if the model wraps JSON despite instructions
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence    = trimmed.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                return trimmed[(firstNewline + 1)..lastFence].Trim();
        }
        return trimmed;
    }

    // DTO for deserializing the generation response — not exposed outside this class
    private sealed class GeneratedTestInputDto
    {
        [JsonPropertyName("inputId")]
        public string? InputId { get; set; }
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        [JsonPropertyName("userMessage")]
        public string? UserMessage { get; set; }
        [JsonPropertyName("expectedBehavior")]
        public string? ExpectedBehavior { get; set; }
    }
}
