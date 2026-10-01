using PIPDC.Domain.Common;

namespace PIPDC.Application.Contact;

public interface IContactService
{
    /// <summary>
    /// Accepts a public contact-form submission and forwards it to the
    /// configured recipient by email.
    /// </summary>
    /// <remarks>
    /// Delivery is queued rather than awaited, so a mail failure can never fail
    /// the visitor's request. The response therefore acknowledges receipt, not
    /// delivery.
    /// </remarks>
    Task<Result> SubmitAsync(ContactRequest request, CancellationToken ct);
}
