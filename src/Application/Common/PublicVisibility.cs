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
    /// Agents that may appear in the public agent directory. A suspended agent
    /// stays in the database and keeps all of their enquiries, conversations and
    /// properties; they are only hidden from public surfaces.
    /// </summary>
    public static IQueryable<Agent> VisibleAgents(this IQueryable<Agent> query) =>
        query.Where(a => !a.IsSuspended);

    /// <summary>
    /// Properties that may appear in public listings. Properties with no assigned
    /// agent are still shown — they are not moderated by an agent decision.
    /// </summary>
    public static IQueryable<Property> VisibleProperties(this IQueryable<Property> query) =>
        query.Where(p => p.Agent == null || !p.Agent.IsSuspended);
}
