namespace PIPDC.Application.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? ToName = null,
    string? TextBody = null)
{
    /// <summary>
    /// Adds RFC 8058 <c>List-Unsubscribe</c> and <c>List-Unsubscribe-Post</c>
    /// headers, which declare the message to be bulk or marketing mail.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <c>false</c> because every template in this codebase is
    /// individually triggered transactional mail sent to one specific recipient.
    /// There is no bulk or campaign send.
    /// </para>
    /// <para>
    /// This was previously <c>true</c>, and the transactional security templates
    /// (email verification, password reset, password changed) did not override
    /// it. Those messages therefore carried <c>List-Unsubscribe-Post:
    /// List-Unsubscribe=One-Click</c>, an explicit declaration of bulk send, on a
    /// one-time security code. Mailbox providers treat that mismatch as a strong
    /// spam signal and filtered the mail into spam.
    /// </para>
    /// <para>
    /// A genuine bulk or campaign template must opt in explicitly, and should
    /// only do so once a real sending domain exists so SPF and DKIM can be
    /// published for it.
    /// </para>
    /// </remarks>
    public bool IncludeUnsubscribe { get; init; }

    /// <summary>
    /// Optional address that replies to this message are routed to, instead of
    /// <see cref="To"/>. Used by the contact form so replying to the
    /// notification reaches the sender rather than the no-reply account.
    /// </summary>
    public string? ReplyTo { get; init; }
}