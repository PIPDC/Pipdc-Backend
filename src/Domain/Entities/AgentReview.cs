using PIPDC.Domain.Common;

namespace PIPDC.Domain.Entities;

/// <summary>
/// A client's rating of, and comment on, an agent.
///
/// One review per reviewer per agent: the unique index on
/// (AgentId, ReviewerUserId) makes a second submission an update of the existing
/// review rather than a duplicate. This keeps the public average honest without
/// introducing a review-moderation workflow.
/// </summary>
public class AgentReview : AuditableEntity
{
    /// <summary>The reviewed agent. Required.</summary>
    public int AgentId { get; set; }

    /// <summary>
    /// The account that wrote the review, taken from the JWT subject claim.
    /// Required and never supplied by the caller.
    /// </summary>
    public string ReviewerUserId { get; set; } = string.Empty;

    /// <summary>Whole-star rating from 1 to 5. Validated server-side on every write.</summary>
    public int Rating { get; set; }

    /// <summary>Optional free-text comment.</summary>
    public string? Comment { get; set; }

    public Agent Agent { get; set; } = null!;
    public AppUser Reviewer { get; set; } = null!;
}
