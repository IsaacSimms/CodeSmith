// == Dynamic Sessions HTTP Client Name == //
namespace CodeSmith.Infrastructure.Services.DynamicSessions;

// The POST /execute request/response DTOs this Adapter sends live in
// Services/Executor/ExecutorContracts.cs — both Adapters talk to the same
// CodeSmith.Executor image, so the wire contract is shared rather than duplicated.

/// <summary>
/// Shared name for the Dynamic Sessions HttpClient registered via IHttpClientFactory.
/// </summary>
public static class DynamicSessionsHttpClient
{
    public const string Name = "DynamicSessions";
}
