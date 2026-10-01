using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PIPDC.API.Extensions;
using PIPDC.Application.Contact;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.API.Controllers;

/// <summary>
/// Public contact form. Anonymous, so it accepts no JWT and has no user to derive
/// identity from.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/contact")]
[AllowAnonymous]
public class ContactController(IContactService contactService) : ControllerBase
{
    /// <summary>
    /// Accepts a contact-form submission and forwards it to the configured recipient.
    /// </summary>
    /// <remarks>
    /// This endpoint is anonymous and unthrottled by Turnstile, because the widget
    /// is currently disabled while no production domain is owned. It therefore uses
    /// <see cref="RateLimitPolicies.AuthStrict"/>, the same tight per-IP limiter as
    /// login and registration, since it is an unauthenticated POST that sends mail.
    ///
    /// The response acknowledges receipt, not delivery: the message is queued and
    /// sent by the background worker, so a mail outage cannot be reported here
    /// without first storing the submission somewhere it can be retried.
    /// </remarks>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.AuthStrict)]
    public async Task<IActionResult> Submit([FromBody] ContactRequest request, CancellationToken ct)
    {
        var result = await contactService.SubmitAsync(request, ct);

        if (result.IsFailure)
            return result.ToActionResult();

        return Accepted(new { message = "Thanks. Our team will respond within one business day." });
    }
}
