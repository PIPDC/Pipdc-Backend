using PIPDC.Domain.Common;

namespace PIPDC.Domain.Entities;

/// <summary>
/// A permanent bar on an account applying to become an agent again.
/// </summary>
/// <remarks>
/// <para>
/// This is the outcome the platform uses when an application is rejected and the
/// administrator decides the person should not be allowed to simply try again.
/// It is deliberately permanent and deliberately liftable: an administrator who
/// set it can lift it, which is what makes a permanent ban safer to operate than
/// a silent one.
/// </para>
/// <para>
/// The bar is on the <em>account</em>, not on an application, so withdrawing,
/// deleting or re-submitting applications cannot shed it. That is the whole point:
/// otherwise a blocked applicant clears the block by applying and withdrawing in
/// a loop.
/// </para>
/// </remarks>
public class AgentApplicationBlock : AuditableEntity
{
    /// <summary>The blocked account. Never taken from the request body.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The administrator who set the bar, from the JWT subject claim.
    /// </summary>
    /// <remarks>
    /// AuditableEntity has no created-by column, so this entity carries its own.
    /// A permanent bar with no attributable decision-maker is the kind of thing
    /// that becomes impossible to defend later.
    /// </remarks>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Why the bar was set. Shown to the applicant.</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime? LiftedAt { get; set; }

    /// <summary>The administrator who lifted the bar, when it is lifted.</summary>
    public string? LiftedByAdminId { get; set; }

    public AppUser User { get; set; } = null!;
    public AppUser? LiftedByAdmin { get; set; }
}
