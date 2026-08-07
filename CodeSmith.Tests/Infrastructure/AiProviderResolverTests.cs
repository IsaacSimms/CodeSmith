// == AiProviderResolver Tests == //
using CodeSmith.Core.Enums;
using CodeSmith.Core.Exceptions;
using CodeSmith.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CodeSmith.Tests.Infrastructure;

public class AiProviderResolverTests
{
    private static AiProviderResolver CreateResolver(AiProvider activeProvider)
        => new(Options.Create(new AiOptions { ActiveProvider = activeProvider }));

    // == Omitted request → configured ActiveProvider == //

    [Fact]
    public void Resolve_WhenRequestedIsNull_ReturnsActiveProvider()
    {
        var resolver = CreateResolver(AiProvider.Xai);

        var result = resolver.Resolve(requested: null);

        Assert.Equal(AiProvider.Xai, result);
    }

    // == Explicit request honored even when it differs from ActiveProvider == //

    [Fact]
    public void Resolve_WhenRequestedDiffersFromActive_HonorsRequested()
    {
        var resolver = CreateResolver(AiProvider.Xai);

        var result = resolver.Resolve(AiProvider.Anthropic);

        Assert.Equal(AiProvider.Anthropic, result);
    }

    // == Undefined enum value → UnknownProviderException == //

    [Fact]
    public void Resolve_WhenRequestedIsUndefined_ThrowsUnknownProviderException()
    {
        var resolver = CreateResolver(AiProvider.Xai);

        Assert.Throws<UnknownProviderException>(() => resolver.Resolve((AiProvider)999));
    }
}
