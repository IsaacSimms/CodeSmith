// == Scenario Not Found Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a requested scenario does not exist in the System Lab catalog.
/// </summary>
public class ScenarioNotFoundException : Exception
{
    public string ScenarioId { get; }  // The scenario ID that was not found

    public ScenarioNotFoundException(string scenarioId)
        : base($"Scenario '{scenarioId}' not found.")
    {
        ScenarioId = scenarioId;
    }
}
