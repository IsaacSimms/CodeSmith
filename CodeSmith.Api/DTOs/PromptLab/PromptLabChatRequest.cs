// == Prompt Lab Chat Request DTO == //
namespace CodeSmith.Api.DTOs.PromptLab;

public class PromptLabChatRequest
{
    public string  Message       { get; set; } = string.Empty;
    public string? EditorContent { get; set; }  // Optional structured prompt draft: "[SYSTEM PROMPT]\n...\n\n[USER MESSAGE]\n..."
}
