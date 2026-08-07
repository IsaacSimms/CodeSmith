// == Usage Controller == //
using CodeSmith.Api.DTOs.Usage;
using CodeSmith.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeSmith.Api.Controllers;

/// <summary>
/// Free-quota read endpoints for the account surface. Lives outside billing so billing never
/// references <see cref="IUsageEnforcer"/>. Authenticated but never metered — reading quota must not cost quota.
/// </summary>
[ApiController]
[Route("api/usage")]
public class UsageController : ControllerBase
{
    private readonly IUsageEnforcer _enforcer;
    private readonly ICurrentUser _currentUser;

    public UsageController(IUsageEnforcer enforcer, ICurrentUser currentUser)
    {
        _enforcer = enforcer;
        _currentUser = currentUser;
    }

    // == Read: Free quota == //

    [HttpGet("quota")]
    [Authorize]
    [ProducesResponseType(typeof(QuotaResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuota(CancellationToken ct)
    {
        var objectId = RequireObjectId();
        var snapshot = await _enforcer.GetQuotaAsync(objectId, _currentUser.ClientIp, ct);

        return Ok(new QuotaResponse
        {
            FreeTokensUsed = snapshot.FreeTokensUsed,
            FreeQuotaMax = snapshot.FreeQuotaMax,
            IpConstraint = snapshot.IpConstraint
        });
    }

    private string RequireObjectId()
    {
        var objectId = _currentUser.ObjectId;
        if (string.IsNullOrWhiteSpace(objectId))
            throw new InvalidOperationException("An authenticated user is required for this usage operation.");
        return objectId;
    }
}
