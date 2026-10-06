using System.ComponentModel.DataAnnotations;

namespace PIPDC.Application.Agents;

public record AgentDto(
    int Id,
    string? Bio,
    string? Title,
    string? Photo,
    string? PhotoPublicId,
    string Agency,
    string? LicenseNumber,
    string Phone,
    bool Verified,
    string FullName,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int PropertyCount,
    double? AverageRating,
    int ReviewCount,
    bool IsSuspended,
    DateTime? SuspendedAt,
    string? SuspensionReason,
    // Removal (revocation) state. The admin directory needs these to tell a
    // revoked agent apart from a live one; without them the UI cannot compute
    // which agents are removed, so a revoked agent looks like an ordinary
    // listing. The public directory never returns removed agents at all.
    bool IsRemoved,
    DateTime? RemovedAt,
    string? RemovalReason,
    int? ReassignedToAgentId);

public record CreateAgentRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(100)] string? Title,
    [MaxLength(500)] string? PhotoUrl,
    [MaxLength(200)] string? PhotoPublicId,
    [MaxLength(4000)] string? Bio,
    [Required, MaxLength(200)] string AgencyName,
    [MaxLength(100)] string? LicenseNumber,
    [Required, MaxLength(20)] string PhoneNumber);

public record UpdateAgentRequest(
    [MaxLength(100)] string? Title,
    [MaxLength(500)] string? PhotoUrl,
    [MaxLength(200)] string? PhotoPublicId,
    [MaxLength(4000)] string? Bio,
    [Required, MaxLength(200)] string AgencyName,
    [MaxLength(100)] string? LicenseNumber,
    [Required, MaxLength(20)] string PhoneNumber,
    bool IsVerified);

public record AgentSummaryDto(
    int Id,
    string? Bio,
    string? Title,
    string? Photo,
    string? PhotoPublicId,
    string Agency,
    string? LicenseNumber,
    string Phone,
    bool Verified,
    string FullName,
    string UserId,
    string Email,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int PropertyCount,
    int EnquiryCount,
    int ConversationCount,
    double? AverageRating,
    int ReviewCount,
    int OpenReportCount,
    bool IsSuspended,
    DateTime? SuspendedAt,
    string? SuspensionReason,
    bool IsRemoved = false,
    DateTime? RemovedAt = null,
    string? RemovalReason = null,
    int? ReassignedToAgentId = null);

/// <summary>
/// Admin request to revoke an agent registration.
/// </summary>
/// <remarks>
/// <see cref="ReassignToAgentId"/> is optional. When supplied, the removed agent's
/// listings and open enquiries move to that agent so clients are not stranded;
/// when omitted the work stays with the removed agent and drops out of public view.
/// </remarks>
public record RemoveAgentRequest(
    [Required(ErrorMessage = "A reason is required so the agent can be told why.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "The removal reason must be between 10 and 1000 characters.")]
    string Reason,
    int? ReassignToAgentId);

/// <summary>
/// Outcome of a removal, so the admin sees what actually happened to the work.
/// </summary>
public record AgentRemovalResult(
    int AgentId,
    int PropertiesReassigned,
    int EnquiriesReassigned,
    bool Reassigned,
    bool ApplicationRevoked,
    string Message);
