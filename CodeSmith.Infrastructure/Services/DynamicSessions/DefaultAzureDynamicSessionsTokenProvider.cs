// == Default Azure Dynamic Sessions Token Provider == //
using Azure.Core;
using Azure.Identity;

namespace CodeSmith.Infrastructure.Services.DynamicSessions;

/// <summary>
/// Uses DefaultAzureCredential (Managed Identity in Azure, az/login / VS locally)
/// to acquire tokens for the Dynamic Sessions management API.
/// </summary>
public sealed class DefaultAzureDynamicSessionsTokenProvider : IDynamicSessionsTokenProvider
{
    private static readonly string[] Scopes = ["https://dynamicsessions.io/.default"];
    private readonly TokenCredential _credential;

    public DefaultAzureDynamicSessionsTokenProvider(TokenCredential? credential = null)
    {
        _credential = credential ?? new DefaultAzureCredential();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await _credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
        return token.Token;
    }
}
