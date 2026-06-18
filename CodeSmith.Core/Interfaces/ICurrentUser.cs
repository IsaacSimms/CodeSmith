// == Current User Interface == //
namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Provides the authenticated user's stable identifier (Entra objectId) to downstream seams.
/// </summary>
public interface ICurrentUser
{
    string? ObjectId { get; }
}
