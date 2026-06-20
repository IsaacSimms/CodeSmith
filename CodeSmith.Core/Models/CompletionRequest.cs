// == Completion Request == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models;

/// <summary>
/// Everything an LLM adapter needs to perform a single completion, plus the caller intent
/// (tier + feature) that used to be encoded in capability-named methods. Carrying intent as data
/// is what lets one ILlmService.CompleteAsync replace the former seven named methods.
/// Single-turn callers use <see cref="SingleTurn"/>; multi-turn callers pass the full history.
/// </summary>
public sealed record CompletionRequest
{
    public required string SystemPrompt { get; init; }                  // System prompt sent alongside the messages
    public required IReadOnlyList<ChatMessage> Messages { get; init; }   // Conversation turns; single-turn is one User message
    public required ModelTier Tier { get; init; }                       // Caller-chosen model tier (no default — choosing tier is a cost decision)
    public required int MaxTokens { get; init; }                        // Output token budget for this call
    public required string Feature { get; init; }                       // Intent label (e.g. "PromptLab:Evaluate") for error logs + usage ledger

    // Convenience factory for the common single-turn case (system prompt + one user message)
    public static CompletionRequest SingleTurn(string systemPrompt, string userMessage, ModelTier tier, int maxTokens, string feature)
        => new()
        {
            SystemPrompt = systemPrompt,
            Messages     = [new ChatMessage { Role = MessageRole.User, Content = userMessage }],
            Tier         = tier,
            MaxTokens    = maxTokens,
            Feature      = feature
        };
}
