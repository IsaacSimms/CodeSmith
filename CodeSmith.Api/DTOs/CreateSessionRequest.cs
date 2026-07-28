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
    public AiProvider Provider { get; set; }    // The AI provider to use for this session

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProblemFocus Focus { get; set; }     // Approach style; omitted or Random rolls one server-side

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProblemTopic Topic { get; set; }     // Subject area; omitted or Random rolls one server-side
}
