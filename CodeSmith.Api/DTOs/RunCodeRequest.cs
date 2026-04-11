// == Run Code Request DTO == //
using System.ComponentModel.DataAnnotations;
using CodeSmith.Core.Enums;

namespace CodeSmith.Api.DTOs;

/// <summary>
/// Request body for executing user code within a session.
/// </summary>
public class RunCodeRequest
{
    [Required(ErrorMessage = "Code is required.")]
    [StringLength(50000, MinimumLength = 1, ErrorMessage = "Code must be between 1 and 50000 characters.")]
    public string Code { get; set; } = string.Empty; // The user's code to execute

    [Required(ErrorMessage = "Language is required.")]
    public Language Language { get; set; } // The programming language of the code
}
