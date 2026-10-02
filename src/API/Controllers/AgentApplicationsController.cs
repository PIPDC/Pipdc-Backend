using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using PIPDC.API.Extensions;
using PIPDC.Application.Agents;
using PIPDC.Application.Auth;
using PIPDC.Domain.Enums;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Authenticated endpoint for a signed-in user to apply to become a PIPDC agent.
///
/// There is no endpoint here that promotes a user to agent, and no user id in
/// any request body. The applicant is always the bearer of the access token.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/agent-applications")]
public class AgentApplicationsController(IAgentApplicationService applicationService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Submit([FromBody] AgentApplicationRequest request, CancellationToken ct)
    {
        var result = await applicationService.SubmitAsync(CurrentUserId, request, ct);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await applicationService.GetMineAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var result = await applicationService.GetCurrentAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Withdraws the caller's own rejected application so they can reapply from
    /// scratch. Scoped to the token, so it can only ever delete the caller's own
    /// row, and refuses anything not in the Rejected state.
    /// </summary>
    [HttpDelete("mine")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> WithdrawRejected(CancellationToken ct)
    {
        var result = await applicationService.WithdrawRejectedAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    // ── Admin review ─────────────────────────────────────────────────────
    //
    // Every route below is admin-only. The applicant surface above is the only
    // place a user id is read from the token; these routes take an explicit
    // admin id, also from the token, so the reviewing admin is always the
    // authenticated caller and never a value from the request.

    /// <summary>Admin listing of applications, newest first.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ListForReview(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] AgentApplicationStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await applicationService.ListForReviewAsync(
            new PagedQuery(page, pageSize, status), ct);

        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetForReview(int id, CancellationToken ct)
    {
        var result = await applicationService.GetForReviewAsync(id, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Claims an application for review, so two admins do not work the same one.
    /// </summary>
    [HttpPost("{id:int}/start-review")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> StartReview(int id, CancellationToken ct)
    {
        var result = await applicationService.StartReviewAsync(id, CurrentUserId, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Approves an application: grants the Agent role and issues a licence. Does
    /// not verify; verification is a separate admin step.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        var result = await applicationService.ApproveAsync(id, CurrentUserId, ct);
        return result.ToActionResult();
    }

    /// <summary>Rejects an application with a reason shown to the applicant.</summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Reject(int id, [FromBody] RejectAgentApplicationRequest request, CancellationToken ct)
    {
        var result = await applicationService.RejectAsync(id, CurrentUserId, request.Reason, ct);
        return result.ToActionResult();
    }

    /// <summary>Rejects an application and bars the account from applying again.</summary>
    /// <remarks>
    /// The bar is permanent and separate from the rejection, so it can only be
    /// released by an explicit lift. The applicant is emailed both facts.
    /// </remarks>
    [HttpPost("{id:int}/block")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> Block(int id, [FromBody] BlockAgentApplicationRequest request, CancellationToken ct)
    {
        var result = await applicationService.BlockFromApplyingAsync(id, CurrentUserId, request.Reason, ct);
        return result.ToActionResult();
    }

    // ── Eligibility, appeals and bars ────────────────────────────────────

    /// <summary>
    /// Whether the caller may submit an application right now, and why not. The
    /// frontend uses this to hide the form instead of letting the user fill it in
    /// only to be refused.
    /// </summary>
    [HttpGet("eligibility")]
    public async Task<IActionResult> GetEligibility(CancellationToken ct)
    {
        var result = await applicationService.GetEligibilityAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    [HttpGet("appeals/mine")]
    public async Task<IActionResult> GetMyAppeals(CancellationToken ct)
    {
        var result = await applicationService.GetMyAppealsAsync(CurrentUserId, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Appeals the revocation of the caller's own registration. Scoped to the
    /// token: the application id in the body is checked to belong to the caller
    /// rather than trusted as an ownership claim.
    /// </summary>
    [HttpPost("appeals")]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> SubmitAppeal([FromBody] SubmitAgentAppealRequest request, CancellationToken ct)
    {
        var result = await applicationService.SubmitAppealAsync(CurrentUserId, request, ct);
        return result.ToActionResult();
    }

    [HttpGet("appeals")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ListAppeals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] AgentAppealStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await applicationService.ListAppealsAsync(
            new AgentAppealQuery(page, pageSize, status), ct);

        return result.ToActionResult();
    }

    [HttpGet("appeals/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> GetAppeal(int id, CancellationToken ct)
    {
        var result = await applicationService.GetAppealAsync(id, ct);
        return result.ToActionResult();
    }

    [HttpPost("appeals/{id:int}/start-review")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> StartAppealReview(int id, CancellationToken ct)
    {
        var result = await applicationService.StartAppealReviewAsync(id, CurrentUserId, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Decides an appeal. An upheld appeal reinstates the same registration and
    /// restores the Agent role; a refused one does not, and the applicant is told
    /// whether they may reapply.
    /// </summary>
    [HttpPost("appeals/{id:int}/decide")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> DecideAppeal(
        int id,
        [FromBody] ResolveAgentAppealRequest request,
        CancellationToken ct)
    {
        var result = await applicationService.ResolveAppealAsync(id, CurrentUserId, request, ct);
        return result.ToActionResult();
    }

    /// <summary>Lists the accounts barred from applying, lifted ones included.</summary>
    [HttpGet("blocks")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ListBlocks(CancellationToken ct)
    {
        var result = await applicationService.ListApplicationBlocksAsync(ct);
        return result.ToActionResult();
    }

    /// <summary>Lifts a permanent bar, letting the account apply again.</summary>
    [HttpPost("blocks/{userId}/lift")]
    [Authorize(Roles = Roles.Admin)]
    [EnableRateLimiting(RateLimitPolicies.Writes)]
    public async Task<IActionResult> LiftBlock(string userId, CancellationToken ct)
    {
        var result = await applicationService.LiftApplicationBlockAsync(userId, CurrentUserId, ct);
        return result.ToActionResult();
    }
}
