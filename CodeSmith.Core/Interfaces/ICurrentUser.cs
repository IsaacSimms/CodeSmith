// == Current User Interface == //
namespace CodeSmith.Core.Interfaces;

/// <summary>
/// Provides the authenticated user's stable identifier (Entra objectId) and client IP (for usage/IP caps).
/// </summary>
public interface ICurrentUser
{
    string? ObjectId { get; }

    string? ClientIp { get; }
}
