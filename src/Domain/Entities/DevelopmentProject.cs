using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;

namespace PIPDC.Domain.Entities;

public class DevelopmentProject : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int? LocationRefId { get; set; }
    public Location? LocationRef { get; set; }
    public string? Developer { get; set; }
    public DevelopmentProjectStatus Status { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public int ProgressPercentage { get; set; }
    public bool Featured { get; set; }

    /// <summary>
    /// The property that represents this development on the public site, if one
    /// has been linked. Nullable, because a project may exist before any listing
    /// is associated with it.
    /// </summary>
    /// <remarks>
    /// This is the link the supervisor's listing prompt acts on: when a project
    /// reaches Completed, the admin is notified and may explicitly promote the
    /// linked property. One property may represent at most one project, so the
    /// foreign key is unique. Deleting the property clears the link rather than
    /// deleting the project.
    /// </remarks>
    public int? PropertyId { get; set; }
    public Property? Property { get; set; }

    public ICollection<DevelopmentUnit> Units { get; set; } = [];
    public ICollection<DevelopmentUpdate> Updates { get; set; } = [];
    public ICollection<DevelopmentProjectImage> Images { get; set; } = [];
    public ICollection<DevelopmentTracking> TrackedBy { get; set; } = [];
}
