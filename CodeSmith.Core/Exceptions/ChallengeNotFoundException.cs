// == Challenge Not Found Exception == //
namespace CodeSmith.Core.Exceptions;

/// <summary>
/// Thrown when a requested challenge does not exist in the catalog.
/// </summary>
public class ChallengeNotFoundException : Exception
{
    public string ChallengeId { get; }  // The challenge ID that was not found

    public ChallengeNotFoundException(string challengeId)
        : base($"Challenge '{challengeId}' not found.")
    {
        ChallengeId = challengeId;
    }
}
