// == Evaluation Parse Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when the evaluator LLM response cannot be parsed into a ScenarioAttempt.
/// Propagates to the service layer, which wraps it as AiServiceException without persisting a poisoned attempt.
/// </summary>
public class EvaluationParseException : Exception
{
    public EvaluationParseException(string message) : base(message) { }
    public EvaluationParseException(string message, Exception innerException) : base(message, innerException) { }
}
