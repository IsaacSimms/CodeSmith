// == Create Session Request DTO == //
using System.Text.Json.Serialization;
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs;

/// <summary>
/// Request body for creating a new problem session.
/// </summary>
public class CreateSessionRequest
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Difficulty Difficulty { get; set; }  // The desired difficulty level for the coding problem

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Language Language { get; set; }      // The desired programming language for the coding problem

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AiProvider? Provider { get; set; }   // Optional: the AI provider to use. Defaults to the server's configured active provider.
}
