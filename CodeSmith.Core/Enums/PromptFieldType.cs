// == Prompt Field Type Enum == //
namespace CodeSmith.Core.Enums;

/// <summary>
/// Identifies which part of a prompt a field belongs to.
/// </summary>
public enum PromptFieldType
{
    SystemPrompt, // The system-level instruction given to the model
    UserMessage   // The user-turn message sent to the model
}
