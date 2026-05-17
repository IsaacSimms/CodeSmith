// == Problem Response Parser Implementation == //
using CodeSmith.Core.Interfaces;

namespace CodeSmith.Infrastructure.Services;

/// <summary>
/// Parses the structured DESCRIPTION / STARTER_CODE format produced by the tutoring LLM.
/// Stateless — safe to register as singleton.
/// </summary>
public class ProblemResponseParser : IProblemResponseParser
{
    public (string Description, string StarterCode) Parse(string responseText)
    {
        var descIndex = responseText.IndexOf("DESCRIPTION:", StringComparison.OrdinalIgnoreCase);
        var codeIndex = responseText.IndexOf("STARTER_CODE:", StringComparison.OrdinalIgnoreCase);

        if (descIndex >= 0 && codeIndex >= 0)
        {
            var description = responseText[(descIndex + "DESCRIPTION:".Length)..codeIndex].Trim();
            var starterCode = responseText[(codeIndex + "STARTER_CODE:".Length)..].Trim();
            return (description, starterCode);
        }

        // Fallback: treat entire response as description when the expected markers are absent
        return (responseText.Trim(), string.Empty);
    }
}
