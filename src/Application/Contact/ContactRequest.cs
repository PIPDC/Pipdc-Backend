using PIPDC.Domain.Common;

namespace PIPDC.Application.Contact;

/// <summary>
/// A message submitted through the public contact form.
/// </summary>
/// <remarks>
/// Validation lives here rather than in the controller so the service cannot be
/// called with unvalidated input from anywhere else, and so the length limits
/// apply no matter which entry point reaches the service.
/// </remarks>
public sealed record ContactRequest(
    string Name,
    string Email,
    string? Phone,
    string Subject,
    string Message)
{
    public const int MaxNameLength = 120;
    public const int MaxEmailLength = 256;
    public const int MaxPhoneLength = 32;
    public const int MaxSubjectLength = 150;
    public const int MaxMessageLength = 4000;

    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return Required("contact.name.required", "Enter your name.");

        if (Name.Trim().Length > MaxNameLength)
            return TooLong("contact.name.toolong", "Your name is too long.", MaxNameLength);

        if (string.IsNullOrWhiteSpace(Email))
            return Required("contact.email.required", "Enter your email address.");

        // Deliberately not using a strict address-parsing helper: the value only
        // has to be shaped like an address, and it is the recipient's problem to
        // judge deliverability. Over-strict validation here would reject valid
        // addresses the visitor genuinely controls.
        if (Email.Trim().Length > MaxEmailLength || !LooksLikeEmail(Email))
            return Required("contact.email.invalid", "Enter a valid email address.");

        if (!string.IsNullOrWhiteSpace(Phone) && Phone.Trim().Length > MaxPhoneLength)
            return TooLong("contact.phone.toolong", "Your phone number is too long.", MaxPhoneLength);

        if (string.IsNullOrWhiteSpace(Subject))
            return Required("contact.subject.required", "Enter a subject.");

        if (Subject.Trim().Length > MaxSubjectLength)
            return TooLong("contact.subject.toolong", "Your subject is too long.", MaxSubjectLength);

        if (string.IsNullOrWhiteSpace(Message))
            return Required("contact.message.required", "Enter a message.");

        if (Message.Trim().Length > MaxMessageLength)
            return TooLong("contact.message.toolong", "Your message is too long.", MaxMessageLength);

        return Result.Success();
    }

    private static bool LooksLikeEmail(string value)
    {
        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@');

        return at > 0
            && at < trimmed.Length - 1
            && at == trimmed.LastIndexOf('@')
            && !trimmed.Contains(' ')
            && trimmed.IndexOf('.', at) > at;
    }

    private static Result Required(string code, string message) =>
        Result.Failure(Error.Validation(code, message));

    private static Result TooLong(string code, string message, int max) =>
        Result.Failure(Error.Validation(code, $"{message} Maximum {max} characters."));
}
