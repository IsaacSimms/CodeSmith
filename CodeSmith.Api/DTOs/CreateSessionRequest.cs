// == Create Session Request DTO == //
using System.Text.Json.Serialization;
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs;

/// <summary>
/// Request body for creating a new problem session.
/// </summary>
public class CreateSessionRequest
{
    /// <summary>The desired difficulty level for the coding problem.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Difficulty Difficulty { get; set; }
}
