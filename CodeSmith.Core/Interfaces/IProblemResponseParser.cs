// == Problem Response Parser Interface == //
namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Parses a raw LLM response string into its DESCRIPTION and STARTER_CODE sections.
/// </summary>
public interface IProblemResponseParser
{
    // Extracts (Description, StarterCode) from a structured LLM response; falls back gracefully when markers are absent
    (string Description, string StarterCode) Parse(string responseText);
}
