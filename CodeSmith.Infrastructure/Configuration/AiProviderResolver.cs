// == AI Provider Resolver == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using Microsoft.Extensions.Options;

namespace CodeSmith.Infrastructure.Configuration;

/// <summary>
/// Resolves the effective AiProvider for a request: an omitted value becomes
/// <see cref="AiOptions.ActiveProvider"/>; an explicit value is honored after
/// IsDefined validation. Sealed concrete class by design — no interface, so
/// controller tests exercise the real rule rather than a mock.
/// </summary>
public sealed class AiProviderResolver
{
    private readonly AiOptions _options;

    public AiProviderResolver(IOptions<AiOptions> options)
    {
        _options = options.Value;
    }

    // == Resolve == //

    // Null  → ActiveProvider. Defined non-null → that value. Undefined → UnknownProviderException.
    public AiProvider Resolve(AiProvider? requested)
    {
        if (requested is null)
            return _options.ActiveProvider;

        if (!Enum.IsDefined(requested.Value))
            throw new UnknownProviderException((int)requested.Value);

        return requested.Value;
    }
}
