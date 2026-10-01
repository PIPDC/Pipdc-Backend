using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

public record AgentApplicationRequest(
    string FullName,
    string StateOfOrigin,
    string ResidentialAddress,
    string LocalGovernmentArea,
    string PhoneNumber,
    string? AgencyName,
    string? AdditionalNotes);

public record AgentApplicationResponse(
    int Id,
    AgentApplicationStatus Status,
    DateTime CreatedAt,
    string FullName,
    string StateOfOrigin,
    string ResidentialAddress,
    string LocalGovernmentArea,
    string PhoneNumber,
    string? AgencyName,
    string? AdditionalNotes,
    DateTime? ReviewedAt,
    string? RejectionReason);

public interface IAgentApplicationService
{
    /// <summary>
    /// Records an application for the supplied authenticated user. The user id is
    /// never taken from the request; it is supplied by the API layer from the JWT.
    /// </summary>
    Task<Result<AgentApplicationResponse>> SubmitAsync(
        string userId,
        AgentApplicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the caller's own applications, newest first.</summary>
    Task<Result<IReadOnlyList<AgentApplicationResponse>>> GetMineAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the caller's most recent open application, if any.</summary>
    Task<Result<AgentApplicationResponse?>> GetCurrentAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
