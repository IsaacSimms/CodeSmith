// == No-op Current User (for Infra DI bootstrap; Api replaces with real) == //
using CodeSmith.Core.Interfaces;

namespace CodeSmith.Infrastructure.Services.Usage;

internal sealed class NoopCurrentUser : ICurrentUser
{
    public string? ObjectId => null;
}
