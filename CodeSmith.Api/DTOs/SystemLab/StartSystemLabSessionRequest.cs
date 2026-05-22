// == Start System Lab Session Request DTO == //
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs.SystemLab;

public class StartSystemLabSessionRequest
{
    public string ScenarioId { get; set; } = string.Empty;
    public AiProvider Provider { get; set; } = AiProvider.Anthropic;
}
