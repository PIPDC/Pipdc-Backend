using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

/// <summary>
/// A client report against an agent, raised through the public report action.
///
/// The reporter is always the authenticated user who filed it. There is no
/// client-supplied reporter id on any request record, so a caller cannot file a
/// report in someone else's name.
/// </summary>
public class AgentReport : AuditableEntity
{
    /// <summary>The reported agent. Required.</summary>
    public int AgentId { get; set; }

    /// <summary>
    /// The account that filed the report, taken from the JWT subject claim.
    /// Required and never supplied by the caller.
    /// </summary>
    public string ReporterUserId { get; set; } = string.Empty;

    public AgentReportReason Reason { get; set; }

    /// <summary>The reporter's account of what happened. Required.</summary>
    public string Description { get; set; } = string.Empty;

    public AgentReportStatus Status { get; set; } = AgentReportStatus.Open;

    /// <summary>When an administrator last moved the report out of <see cref="AgentReportStatus.Open"/>.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>The administrator who triaged the report, from the JWT subject claim.</summary>
    public string? ReviewedByAdminId { get; set; }

    /// <summary>
    /// The administrator's closing note. Required when the report is resolved or
    /// dismissed, and optional while it is still open.
    /// </summary>
    public string? ResolutionNote { get; set; }

    public Agent Agent { get; set; } = null!;
    public AppUser Reporter { get; set; } = null!;
    public AppUser? ReviewedByAdmin { get; set; }
}
