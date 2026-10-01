using Microsoft.EntityFrameworkCore;
using PIPDC.Application.Agents;
using PIPDC.Application.Data;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

/// <summary>
/// Owns the authenticated agent-application boundary.
///
/// This service deliberately exposes only the applicant's own view. There is no
/// admin approve/reject path here on purpose: the review workflow, license
/// generation and admin UI belong to a later batch. What is established now is
/// that an application can only ever be created for, and read back by, the
/// authenticated user it belongs to.
/// </summary>
public class AgentApplicationService(IAppDbContext dbContext) : IAgentApplicationService
{
    public async Task<Result<AgentApplicationResponse>> SubmitAsync(
        string userId,
        AgentApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentApplicationResponse>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required to apply."));

        var validation = Validate(request);
        if (validation is not null)
            return Result<AgentApplicationResponse>.Failure(validation);

        if (await dbContext.Agents.AnyAsync(a => a.UserId == userId, cancellationToken))
            return Result<AgentApplicationResponse>.Failure(
                Error.Conflict("agentapplication.alreadyagent", "This account is already registered as an agent."));

        var hasOpen = await dbContext.AgentApplications.AnyAsync(
            a => a.UserId == userId
                && (a.Status == AgentApplicationStatus.Submitted || a.Status == AgentApplicationStatus.UnderReview),
            cancellationToken);

        if (hasOpen)
            return Result<AgentApplicationResponse>.Failure(
                Error.Conflict("agentapplication.openexists", "You already have an application awaiting review."));

        var application = new AgentApplication
        {
            UserId = userId,
            FullName = request.FullName.Trim(),
            StateOfOrigin = request.StateOfOrigin.Trim(),
            ResidentialAddress = request.ResidentialAddress.Trim(),
            LocalGovernmentArea = request.LocalGovernmentArea.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            AgencyName = Normalize(request.AgencyName),
            AdditionalNotes = Normalize(request.AdditionalNotes),
            Status = AgentApplicationStatus.Submitted,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.AgentApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AgentApplicationResponse>.Success(Map(application));
    }

    public async Task<Result<IReadOnlyList<AgentApplicationResponse>>> GetMineAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<IReadOnlyList<AgentApplicationResponse>>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var items = await dbContext.AgentApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<AgentApplicationResponse> responses = items.Select(Map).ToList();
        return Result<IReadOnlyList<AgentApplicationResponse>>.Success(responses);
    }

    public async Task<Result<AgentApplicationResponse?>> GetCurrentAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return Result<AgentApplicationResponse?>.Failure(
                Error.Unauthorized("agentapplication.unauthenticated", "An authenticated account is required."));

        var application = await dbContext.AgentApplications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<AgentApplicationResponse?>.Success(application is null ? null : Map(application));
    }

    private static Error? Validate(AgentApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Error.Validation("agentapplication.fullname", "Full name is required.");

        if (request.FullName.Trim().Length > 200)
            return Error.Validation("agentapplication.fullname", "Full name must be 200 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.StateOfOrigin))
            return Error.Validation("agentapplication.stateoforigin", "State of origin is required.");

        if (string.IsNullOrWhiteSpace(request.ResidentialAddress))
            return Error.Validation("agentapplication.address", "Residential address is required.");

        if (string.IsNullOrWhiteSpace(request.LocalGovernmentArea))
            return Error.Validation("agentapplication.lga", "Local government area is required.");

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return Error.Validation("agentapplication.phone", "Phone number is required.");

        if (request.PhoneNumber.Trim().Length > 20)
            return Error.Validation("agentapplication.phone", "Phone number must be 20 characters or fewer.");

        if (request.AgencyName is { Length: > 200 })
            return Error.Validation("agentapplication.agencyname", "Agency name must be 200 characters or fewer.");

        if (request.AdditionalNotes is { Length: > 2000 })
            return Error.Validation("agentapplication.notes", "Additional notes must be 2000 characters or fewer.");

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AgentApplicationResponse Map(AgentApplication a) => new(
        a.Id,
        a.Status,
        a.CreatedAt,
        a.FullName,
        a.StateOfOrigin,
        a.ResidentialAddress,
        a.LocalGovernmentArea,
        a.PhoneNumber,
        a.AgencyName,
        a.AdditionalNotes,
        a.ReviewedAt,
        a.RejectionReason);
}
