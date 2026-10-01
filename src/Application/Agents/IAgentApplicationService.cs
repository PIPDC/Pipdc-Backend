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
    // Serialized as a name, not the enum's integer, to match every other status
    // DTO in the codebase. Without this the API would emit "status": 0.
    string Status,
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

/// <summary>Admin-facing projection of an application, with the applicant attached.</summary>
public record AgentApplicationReviewResponse(
    int Id,
    string Status,
    DateTime CreatedAt,
    string UserId,
    string ApplicantEmail,
    string? ApplicantEmailConfirmed,
    string FullName,
    string StateOfOrigin,
    string ResidentialAddress,
    string LocalGovernmentArea,
    string PhoneNumber,
    string? AgencyName,
    string? AdditionalNotes,
    DateTime? ReviewedAt,
    string? ReviewedByAdminId,
    string? RejectionReason);

/// <summary>A page of applications plus the total count matching the filter.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public record RejectAgentApplicationRequest(string Reason);

public record PagedQuery(int Page = 1, int PageSize = 20, AgentApplicationStatus? Status = null);

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

    // ── Admin review workflow ────────────────────────────────────────────
    //
    // Approval is a two-stage outcome and the two stages are deliberately
    // separate operations:
    //
    //   ApproveAsync  grants the Agent role and issues a licence, but leaves
    //                  IsVerified false. The applicant becomes able to act as an
    //                  agent, but their profile is not yet trusted.
    //   VerifyAsync   flips IsVerified true and is what makes the agent publicly
    //                  trusted.
    //
    // Collapsing these into one step would mean an admin who clicks "approve"
    // also publishes the person's licence to the public directory, which is not
    // a decision the same click should be making.

    /// <summary>Admin listing, newest first, optionally filtered by status.</summary>
    Task<Result<PagedResult<AgentApplicationReviewResponse>>> ListForReviewAsync(
        PagedQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Admin read of a single application.</summary>
    Task<Result<AgentApplicationReviewResponse>> GetForReviewAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an application from Submitted to UnderReview, marking it as claimed
    /// so two admins do not review the same application simultaneously.
    /// </summary>
    Task<Result<AgentApplicationReviewResponse>> StartReviewAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves an application: grants the Agent role, creates the Agent record
    /// with a unique licence, and emails the applicant. Verification is separate.
    /// </summary>
    Task<Result<AgentApplicationReviewResponse>> ApproveAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Rejects an application, recording a reason shown to the applicant.</summary>
    Task<Result<AgentApplicationReviewResponse>> RejectAsync(
        int id,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default);
}
