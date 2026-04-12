// == Piston HTTP Contracts == //
using System.Text.Json.Serialization;

namespace CodeSmith.Infrastructure.Services.Piston;

/// <summary>
/// Request/response DTOs matching Piston's public /api/v2/execute contract.
/// Shape is deliberately aligned 1:1 with Piston so future features
/// (stdin, args, multi-file submissions) only need to populate additional
/// fields rather than change any types.
/// </summary>
internal sealed class PistonExecuteRequest
{
    [JsonPropertyName("language")]          public string Language { get; set; } = "";
    [JsonPropertyName("version")]           public string Version { get; set; } = "*";
    [JsonPropertyName("files")]             public List<PistonFile> Files { get; set; } = new();
    [JsonPropertyName("stdin")]             public string? Stdin { get; set; }
    [JsonPropertyName("args")]              public List<string>? Args { get; set; }
    [JsonPropertyName("compile_timeout")]   public int? CompileTimeout { get; set; }
    [JsonPropertyName("run_timeout")]       public int? RunTimeout { get; set; }
    [JsonPropertyName("compile_memory_limit")] public long? CompileMemoryLimit { get; set; }
    [JsonPropertyName("run_memory_limit")]     public long? RunMemoryLimit { get; set; }
}

internal sealed class PistonFile
{
    [JsonPropertyName("name")]    public string? Name { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

internal sealed class PistonExecuteResponse
{
    [JsonPropertyName("language")] public string Language { get; set; } = "";
    [JsonPropertyName("version")]  public string Version { get; set; } = "";
    [JsonPropertyName("run")]      public PistonStage Run { get; set; } = new();
    [JsonPropertyName("compile")]  public PistonStage? Compile { get; set; }
}

internal sealed class PistonStage
{
    [JsonPropertyName("stdout")] public string Stdout { get; set; } = "";
    [JsonPropertyName("stderr")] public string Stderr { get; set; } = "";
    [JsonPropertyName("output")] public string Output { get; set; } = "";
    [JsonPropertyName("code")]   public int? Code { get; set; }
    [JsonPropertyName("signal")] public string? Signal { get; set; }
}
