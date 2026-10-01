using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PIPDC.Application.Email;
using PIPDC.Domain.Common;

namespace PIPDC.Application.Contact;

/// <summary>
/// Forwards contact-form submissions to the configured recipient.
/// </summary>
/// <remarks>
/// The recipient comes from <see cref="EmailSettings.ContactRecipient"/> so it can
/// be changed without a code change or redeploy. If it is unset the submission is
/// still acknowledged rather than erroring, because telling a member of the public
/// that our inbox is misconfigured is not useful to them and invites probing. The
/// misconfiguration is logged at Error instead.
/// </remarks>
public sealed class ContactService(
    IEmailQueue emailQueue,
    IOptions<EmailSettings> emailSettings,
    ILogger<ContactService> logger) : IContactService
{
    private readonly EmailSettings _settings = emailSettings.Value;

    public Task<Result> SubmitAsync(ContactRequest request, CancellationToken ct)
    {
        var validation = request.Validate();
        if (validation.IsFailure)
            return Task.FromResult(validation);

        if (string.IsNullOrWhiteSpace(_settings.ContactRecipient))
        {
            logger.LogError(
                "Contact form submission discarded: Email:ContactRecipient is not configured. " +
                "Set it in user secrets or appsettings. Submission was from {Email}.", request.Email);
            return Task.FromResult(Result.Success());
        }

        emailQueue.QueueEmail(
            logger,
            EmailTemplates.ContactFormMessage(
                _settings.ContactRecipient,
                request.Name,
                request.Email,
                request.Phone,
                request.Subject,
                request.Message),
            $"contact:{request.Email}",
            ct);

        return Task.FromResult(Result.Success());
    }
}
