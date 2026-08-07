// == Usage Controller Tests == //
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeSmith.Api.Controllers;
using CodeSmith.Api.DTOs.Usage;
using CodeSmith.Core.Enums;
using CodeSmith.Core.Interfaces;
using CodeSmith.Core.Models.Usage;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CodeSmith.Tests.Api;

public class UsageControllerTests
{
    private readonly IUsageEnforcer _enforcer = Substitute.For<IUsageEnforcer>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly UsageController _controller;

    public UsageControllerTests()
    {
        _currentUser.ObjectId.Returns("user-1");
        _currentUser.ClientIp.Returns("10.0.0.1");
        _controller = new UsageController(_enforcer, _currentUser);
    }

    // == GetQuota == //

    [Fact]
    public async Task GetQuota_ReturnsOkWithThreeFields()
    {
        _enforcer.GetQuotaAsync("user-1", "10.0.0.1", Arg.Any<CancellationToken>())
            .Returns(new QuotaSnapshot(1_200, 20_000, IpConstraint.Limited));

        var result = Assert.IsType<OkObjectResult>(await _controller.GetQuota(CancellationToken.None));
        var body = Assert.IsType<QuotaResponse>(result.Value);

        Assert.Equal(1_200, body.FreeTokensUsed);
        Assert.Equal(20_000, body.FreeQuotaMax);
        Assert.Equal(IpConstraint.Limited, body.IpConstraint);
    }

    [Fact]
    public async Task GetQuota_SuppliesObjectIdAndClientIpFromCurrentUser()
    {
        _enforcer.GetQuotaAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new QuotaSnapshot(0, 20_000, IpConstraint.None));

        await _controller.GetQuota(CancellationToken.None);

        await _enforcer.Received(1).GetQuotaAsync("user-1", "10.0.0.1", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(IpConstraint.None)]
    [InlineData(IpConstraint.Limited)]
    [InlineData(IpConstraint.Exhausted)]
    public async Task GetQuota_SerializesIpConstraintAsStringNeverNumber(IpConstraint constraint)
    {
        _enforcer.GetQuotaAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new QuotaSnapshot(0, 20_000, constraint));

        var result = Assert.IsType<OkObjectResult>(await _controller.GetQuota(CancellationToken.None));
        var body = Assert.IsType<QuotaResponse>(result.Value);

        // Same converter the API registers globally — pin the wire shape the contract promises.
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });

        Assert.Contains($"\"ipConstraint\":\"{constraint}\"", json);
        Assert.DoesNotContain($"\"ipConstraint\":{(int)constraint}", json);
        // No raw IP token count field ever appears.
        Assert.DoesNotContain("ipFree", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ipRem", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetQuota_HasAuthorizeButNotMeteredAi()
    {
        var method = typeof(UsageController).GetMethod(nameof(UsageController.GetQuota));
        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true));
        Assert.Empty(method.GetCustomAttributes(typeof(CodeSmith.Api.Authorization.MeteredAiAttribute), inherit: true));
    }
}
