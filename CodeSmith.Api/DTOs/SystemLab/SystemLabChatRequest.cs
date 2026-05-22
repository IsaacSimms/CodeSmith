// == System Lab Chat Request DTO == //
namespace CodeSmith.Api.DTOs.SystemLab;

public class SystemLabChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? CurrentJustification { get; set; }  // Optional current draft from the editor
}
