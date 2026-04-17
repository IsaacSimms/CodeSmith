// == Editable Field Model == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Core.Models.PromptLab;

/// <summary>
/// Describes a prompt field the user is allowed to write for a given challenge.
/// </summary>
public class EditableField
{
    public PromptFieldType FieldType { get; set; }             // SystemPrompt or UserMessage
    public string Placeholder { get; set; } = string.Empty;    // Hint text shown in the editor
    public string DefaultValue { get; set; } = string.Empty;   // Pre-filled content (may be empty)
}
