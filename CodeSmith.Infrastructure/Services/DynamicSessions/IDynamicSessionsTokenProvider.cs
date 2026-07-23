// == Dynamic Sessions Token Provider == //
namespace CodeSmith.Infrastructure.Services.DynamicSessions;

/// <summary>
/// Issues Microsoft Entra access tokens for the Dynamic Sessions management API
/// (audience https://dynamicsessions.io).
/// </summary>
public interface IDynamicSessionsTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
