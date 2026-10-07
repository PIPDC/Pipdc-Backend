namespace PIPDC.Application.Idempotency;

/// <summary>
/// Marks an action as idempotent: repeating the same <c>Idempotency-Key</c> header
/// replays the first attempt's stored response instead of re-running the action.
/// Controllers-reference-only metadata; the behaviour lives in the Infrastructure
/// action filter (PIPDC.Infrastructure.Idempotency.IdempotencyActionFilter) that
/// honours this marker, so the API layer never names an Infrastructure type.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute
{
    public const string HeaderName = "Idempotency-Key";
}