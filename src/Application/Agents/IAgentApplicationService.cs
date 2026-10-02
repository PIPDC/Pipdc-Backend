using System.ComponentModel.DataAnnotations;
using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

public record AgentApplicationRequest(
    string FullName,
    string PhoneNumber,
    DateTime? DateOfBirth,
    string StateOfOrigin,
    string LocalGovernmentArea,
    string ResidentialAddress,
    string NationalIdentityNumber,
    int YearsOfExperience,
    string? AgencyName,
    string? AdditionalNotes);

public record AgentApplicationResponse(
    int Id,
    // Serialized as a name, not the enum's integer, to match every other status
    // DTO in the codebase. Without this the API would emit "status": 0.
    string Status,
    DateTime CreatedAt,
    string FullName,
    string PhoneNumber,
    DateTime? DateOfBirth,
    string StateOfOrigin,
    string LocalGovernmentArea,
    string ResidentialAddress,
    string NationalIdentityNumber,
    int? YearsOfExperience,
    string? AgencyName,
    string? AdditionalNotes,
    DateTime? ReviewedAt,
    string? RejectionReason,
    // Present when the registration this application granted was revoked, so the
    // applicant's own page can say so instead of still claiming approval.
    string? RevocationReason = null,
    DateTime? RevokedAt = null);

/// <summary>
/// What the apply page needs to decide whether to show the form at all.
/// </summary>
/// <remarks>
/// The frontend must not be the only thing enforcing a bar: a blocked applicant
/// has to be refused by the API too. This exists so the form can be hidden
/// honestly rather than left on screen to fail on submit.
/// </remarks>
public record AgentApplicationEligibilityResponse(
    bool CanApply,
    bool IsBlocked,
    string? BlockedReason,
    bool IsAgent,
    bool HasOpenApplication,
    bool HasRevokedRegistration,
    bool CanAppeal,
    bool HasOpenAppeal);

/// <summary>The applicant's view of one appeal.</summary>
public record AgentAppealResponse(
    int Id,
    int? AgentApplicationId,
    string Status,
    string Reason,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? DecisionNote);

/// <summary>Admin-facing projection of an appeal, with the appellant attached.</summary>
public record AgentAppealReviewResponse(
    int Id,
    int? AgentApplicationId,
    string UserId,
    string AppellantName,
    string AppellantEmail,
    string Status,
    string Reason,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByAdminId,
    string? DecisionNote);

/// <summary>An administrator's decision on an appeal.</summary>
public record ResolveAgentAppealRequest(
    [Required(ErrorMessage = "A decision is required.")]
    string Decision,
    [Required(ErrorMessage = "A note is required so the agent knows the outcome.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "The decision note must be between 10 and 2000 characters.")]
    string Note);

public record SubmitAgentAppealRequest(
    [Required(ErrorMessage = "Tell us why the decision was wrong.")]
    [StringLength(4000, MinimumLength = 20, ErrorMessage = "Your appeal must be between 20 and 4000 characters.")]
    string Reason);

public record BlockAgentApplicationRequest(
    [Required(ErrorMessage = "A reason is required so the applicant knows why.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "The reason must be between 10 and 1000 characters.")]
    string Reason);

/// <summary>Admin-facing projection of an application, with the applicant attached.</summary>
public record AgentApplicationReviewResponse(
    int Id,
    string Status,
    DateTime CreatedAt,
    string UserId,
    string ApplicantEmail,
    string? ApplicantEmailConfirmed,
    string FullName,
    string PhoneNumber,
    DateTime? DateOfBirth,
    string StateOfOrigin,
    string LocalGovernmentArea,
    string ResidentialAddress,
    string NationalIdentityNumber,
    int? YearsOfExperience,
    string? AgencyName,
    string? AdditionalNotes,
    DateTime? ReviewedAt,
    string? ReviewedByAdminId,
    string? RejectionReason,
    // Carried on the admin projection so a revoked application shows why. The
    // applicant-facing response omits these on purpose; an admin cannot review
    // a revocation without the reason for it.
    string? RevocationReason,
    DateTime? RevokedAt);

/// <summary>A page of applications plus the total count matching the filter.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// The two outcomes of an appeal. Held as constants so the API, the service and
/// the frontend cannot drift into disagreeing about the spelling of a decision.
/// </summary>
public static class AgentAppealDecisions
{
    public const string Upheld = "Upheld";
    public const string Refused = "Refused";
}

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

    /// <summary>
    /// Withdraws the caller's own rejected application so they can reapply from
    /// scratch.
    /// </summary>
    /// <remarks>
    /// Only a rejected application can be withdrawn. An open or approved one is
    /// still live, and deleting it would either destroy an application an admin
    /// is mid-review on, or orphan the licence issued by an approval. Those
    /// return a conflict rather than being silently refused, so the client can
    /// explain why.
    /// </remarks>
    Task<Result> WithdrawRejectedAsync(
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

    // ── Revocation, appeal and the apply bar ─────────────────────────────

    /// <summary>
    /// Whether the caller's account may open or submit the application form, and
    /// why not when it may not. The apply page uses this to hide the fields.
    /// </summary>
    Task<Result<AgentApplicationEligibilityResponse>> GetEligibilityAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's own appeals, newest first.</summary>
    Task<Result<IReadOnlyList<AgentAppealResponse>>> GetMyAppealsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lodges an appeal against the revocation of the caller's registration.
    /// </summary>
    /// <remarks>
    /// Only available while the caller has a revoked registration and no appeal is
    /// already open. An applicant whose appeal was refused may appeal again, or
    /// submit a fresh application.
    /// </remarks>
    Task<Result<AgentAppealResponse>> SubmitAppealAsync(
        string userId,
        SubmitAgentAppealRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Admin listing of appeals, newest first, optionally filtered by status.</summary>
    Task<Result<PagedResult<AgentAppealReviewResponse>>> ListAppealsAsync(
        AgentAppealQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>A single appeal for the admin decision view.</summary>
    Task<Result<AgentAppealReviewResponse>> GetAppealAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>Moves an appeal from Submitted to UnderReview so two admins do not collide.</summary>
    Task<Result<AgentAppealReviewResponse>> StartAppealReviewAsync(
        int id,
        string adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides an appeal. Upholding it reinstates the agent: the same Agent row and
    /// the same licence are restored and the application returns to Approved.
    /// </summary>
    Task<Result<AgentAppealReviewResponse>> ResolveAppealAsync(
        int id,
        string adminId,
        ResolveAgentAppealRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently bars an account from applying to become an agent. The bar is on
    /// the account, not the application, so it survives withdrawal and re-submission.
    /// </summary>
    Task<Result> BlockFromApplyingAsync(
        int applicationId,
        string adminId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lifts a bar, restoring access to the application form. Keyed on the account
    /// rather than an application, because the bar is on the account and a barred
    /// applicant may have withdrawn or been refused more than one application.
    /// </summary>
    Task<Result> LiftApplicationBlockAsync(
        string userId,
        string adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Admin listing of accounts currently barred from applying.</summary>
    Task<Result<IReadOnlyList<AgentApplicationBlockResponse>>> ListApplicationBlocksAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Filter for the admin appeal queue. Null means every status.</summary>
public record AgentAppealQuery(int Page = 1, int PageSize = 20, AgentAppealStatus? Status = null);

/// <summary>Admin-facing projection of an apply bar.</summary>
public record AgentApplicationBlockResponse(
    int Id,
    string UserId,
    string FullName,
    string Email,
    string Reason,
    DateTime CreatedAt,
    string BlockedByAdminId,
    DateTime? LiftedAt,
    string? LiftedByAdminId);
