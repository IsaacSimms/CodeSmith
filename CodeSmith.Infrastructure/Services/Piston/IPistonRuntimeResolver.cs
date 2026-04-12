// == Piston Runtime Resolver Interface == //
namespace CodeSmith.Infrastructure.Services.Piston;

/// <summary>
/// Resolves a Piston language identifier (e.g. "python") to the exact version
/// string of the runtime currently installed in the Piston container
/// (e.g. "3.10.0"). Piston's /api/v2/execute endpoint requires an exact
/// version and rejects the "*" wildcard, so every execute call must resolve
/// the version up-front. The result is cached for the lifetime of the resolver.
/// </summary>
public interface IPistonRuntimeResolver
{
    Task<string> ResolveVersionAsync(string pistonLanguage, CancellationToken ct = default);
}
