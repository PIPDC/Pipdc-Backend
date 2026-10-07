namespace PIPDC.Application.Captcha;

/// <summary>
/// Marks an action as requiring Cloudflare Turnstile human verification.
/// Controllers-reference-only metadata; the behaviour lives in the Infrastructure
/// action filter (PIPDC.Infrastructure.Captcha.VerifyHumanActionFilter) that
/// honours this marker, so the API layer never names an Infrastructure type.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class VerifyHumanAttribute : Attribute;