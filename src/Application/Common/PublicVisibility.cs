using PIPDC.Domain.Entities;

namespace PIPDC.Application.Common;

/// <summary>
/// Reusable predicates for "publicly visible" data.
///
/// Batch 1 introduces these so that public read paths have a single, testable
/// definition of visibility. They are deliberately NOT applied as EF global
/// query filters: a global filter would silently change admin-facing reads and
/// every existing endpoint at once. Each public query opts in explicitly, which
/// keeps the change reviewable and reversible.
/// </summary>
public static class PublicVisibility
{
    /// <summary>
    /// Agents that may appear in the public agent directory. This is the whole
    /// of the PIPDC agent lifecycle that has to be satisfied first: the agent
    /// record exists, the registration has not been revoked, it is not
    /// suspended, and the admin has verified it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>IsVerified</c> is the gate the application workflow already implies.
    /// Approving an application deliberately creates the agent with
    /// <c>IsVerified = false</c> and issues a licence, but does not make the
    /// agent public: verification is a separate admin action. Requiring it here
    /// means the intended sequence is actually enforced -
    /// apply, approve, licence, verify, then public visibility.
    /// </para>
    /// <para>
    /// Without it, any row in <c>Agents</c> that has not been revoked or
    /// suspended appeared publicly even though it had never been verified. That
    /// let an admin-only account carrying an agent row show up in the public
    /// directory, and it published approved-but-unverified applicants before
    /// the admin had checked them.
    /// </para>
    /// <para>
    /// A suspended or removed agent is hidden for a stronger reason: moderation.
    /// Their row, properties, enquiries and conversations are retained, so the
    /// change is reversible - an upheld appeal restores the registration, and an
    /// admin can re-verify to return them to the directory.
    /// </para>
    /// <para>
    /// A deactivated account is hidden for a different reason: <c>Deactivate</c>
    /// is account-level and is described to the administrator as "they will be
    /// unable to sign in until reactivated". An agent whose account cannot be
    /// signed into cannot answer an enquiry, and publishing them in the directory
    /// still invites clients to contact them. Locking out an account therefore
    /// has to take the agent out of the public directory as well, and
    /// reactivating the account puts them straight back. Only the permanent
    /// lockout set by deactivation counts here - a short lockout after repeated
    /// failed sign-ins is not a reason to drop a working agent from the site.
    /// </para>
    /// </remarks>
    public static IQueryable<Agent> VisibleAgents(this IQueryable<Agent> query) =>
        query.Where(a => a.IsVerified
            && !a.IsSuspended
            && !a.IsRemoved
            && a.User.LockoutEnd != DateTimeOffset.MaxValue);

    /// <summary>
    /// Properties that may appear in public listings. Properties with no assigned
    /// agent are still shown - they are not moderated by an agent decision.
    /// </summary>
    /// <remarks>
    /// A property owned by a removed agent drops out of public listings, so an
    /// admin who removes an agent should reassign the listings they want kept
    /// visible. That is the point of offering reassignment on removal.
    /// </remarks>
    public static IQueryable<Property> VisibleProperties(this IQueryable<Property> query) =>
        query.Where(p => p.Agent == null || (!p.Agent.IsSuspended && !p.Agent.IsRemoved));
}
