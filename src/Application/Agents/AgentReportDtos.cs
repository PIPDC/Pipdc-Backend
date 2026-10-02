using System.ComponentModel.DataAnnotations;
using PIPDC.Domain.Enums;

namespace PIPDC.Application.Agents;

public record AgentReportDto(
    int Id,
    int AgentId,
    string AgentName,
    string AgentAgency,
    string ReporterUserId,
    string ReporterName,
    string ReporterEmail,
    AgentReportReason Reason,
    AgentReportStatus Status,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ReviewedAt,
    string? ReviewedByAdminId,
    string? ResolutionNote);

public record CreateAgentReportRequest(
    [Range(1, int.MaxValue, ErrorMessage = "An agent must be selected.")]
    int AgentId,

    [EnumDataType(typeof(AgentReportReason), ErrorMessage = "Select a valid report reason.")]
    AgentReportReason Reason,

    [Required(ErrorMessage = "Describe what happened.")]
    [StringLength(2000, MinimumLength = 20, ErrorMessage = "The description must be between 20 and 2000 characters.")]
    string Description);

public record UpdateAgentReportStatusRequest(
    [EnumDataType(typeof(AgentReportStatus), ErrorMessage = "Select a valid status.")]
    AgentReportStatus Status,

    [StringLength(1000, ErrorMessage = "The resolution note cannot exceed 1000 characters.")]
    string? ResolutionNote);

public class AgentReportQueryParameters
{
    private const int MaxPageSize = 100;

    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Optional filter. Null means every status, which is what the admin table defaults to.</summary>
    public AgentReportStatus? Status { get; set; }

    /// <summary>Optional filter used by the admin agent view to show one agent's reports.</summary>
    public int? AgentId { get; set; }

    /// <summary>
    /// Optional filter used by the "my reports" route. Set by the API layer from
    /// the JWT subject claim, never bound from the query string, so a caller can
    /// only ever read their own reports through it.
    /// </summary>
    public string? ReporterUserId { get; set; }
}
