namespace PIPDC.Application.Email;

/// <summary>
/// Factory methods that produce <see cref="EmailMessage"/> instances for
/// each notification event.  The application layer calls these; the
/// infrastructure layer (GmailApiEmailService) sends them.
/// </summary>
public static class EmailTemplates
{
    private const string BrandName = "PIPDC";

    public static EmailMessage DevelopmentStatusChanged(string recipientEmail, string recipientName, string projectName, string previousStatus, string newStatus, string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/developments";
        var subject = $"{projectName} status changed to {newStatus}";
        var html = $"<div style=\"font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333\"><h2 style=\"color:#1a5276\">Development Project Update</h2><p>Hi <strong>{Esc(recipientName)}</strong>,</p><p><strong>{Esc(projectName)}</strong> changed from <strong>{Esc(previousStatus)}</strong> to <strong>{Esc(newStatus)}</strong>.</p><a href=\"{ctaUrl}\">View development projects</a><p style=\"font-size:12px;color:#888\">This is an automated notification from {BrandName}.</p></div>";
        var text = $"{projectName} changed from {previousStatus} to {newStatus}. View projects: {ctaUrl}";
        return new EmailMessage(recipientEmail, subject, html, recipientName, text) { IncludeUnsubscribe = false };
    }

    // ── 1. New enquiry → Agent ───────────────────────────────────────────

    public static EmailMessage NewEnquiryToAgent(
        string agentEmail,
        string agentName,
        string clientName,
        string clientMessage,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"New enquiry about {propertyTitle}";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">New Property Enquiry</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p><strong>{Esc(clientName)}</strong> has submitted a new enquiry regarding <strong>{Esc(propertyTitle)}</strong>.</p>
          <blockquote style="border-left:4px solid #1a5276;padding:12px 16px;margin:16px 0;background:#f8f9fa">
            {Esc(clientMessage)}
          </blockquote>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View &amp; Reply to Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        New Property Enquiry

        Hi {agentName},

        {clientName} has submitted a new enquiry regarding {propertyTitle}.

        Message:
        {clientMessage}

        View and reply: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(agentEmail, subject, html, agentName, text) { IncludeUnsubscribe = false };
    }

    // ── 2. Agent reply → Client ──────────────────────────────────────────

    public static EmailMessage AgentReplyToClient(
        string clientEmail,
        string clientName,
        string agentName,
        string messagePreview,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"{agentName} replied to your enquiry about {propertyTitle}";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">New Reply</h2>
          <p>Hi <strong>{Esc(clientName)}</strong>,</p>
          <p><strong>{Esc(agentName)}</strong> has replied to your enquiry about <strong>{Esc(propertyTitle)}</strong>.</p>
          <blockquote style="border-left:4px solid #1a5276;padding:12px 16px;margin:16px 0;background:#f8f9fa">
            {Esc(messagePreview)}
          </blockquote>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View &amp; Reply to Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        New Reply

        Hi {clientName},

        {agentName} has replied to your enquiry about {propertyTitle}.

        Message preview:
        {messagePreview}

        View and reply: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(clientEmail, subject, html, clientName, text) { IncludeUnsubscribe = false };
    }

    // ── 3. Client reply → Agent ──────────────────────────────────────────

    public static EmailMessage ClientReplyToAgent(
        string agentEmail,
        string agentName,
        string clientName,
        string messagePreview,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"{clientName} replied to their enquiry about {propertyTitle}";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">New Reply</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p><strong>{Esc(clientName)}</strong> has replied to the enquiry about <strong>{Esc(propertyTitle)}</strong>.</p>
          <blockquote style="border-left:4px solid #1a5276;padding:12px 16px;margin:16px 0;background:#f8f9fa">
            {Esc(messagePreview)}
          </blockquote>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View &amp; Reply to Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        New Reply

        Hi {agentName},

        {clientName} has replied to the enquiry about {propertyTitle}.

        Message preview:
        {messagePreview}

        View and reply: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(agentEmail, subject, html, agentName, text) { IncludeUnsubscribe = false };
    }

    // ── 4. Viewing scheduled → Client + Agent ────────────────────────────

    public static EmailMessage ViewingScheduledToClient(
        string clientEmail,
        string clientName,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"Viewing scheduled for {propertyTitle}";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Viewing Scheduled</h2>
          <p>Hi <strong>{Esc(clientName)}</strong>,</p>
          <p>A viewing has been scheduled for <strong>{Esc(propertyTitle)}</strong>.</p>
          <p>Please check your conversation for further details from the agent.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Viewing Scheduled

        Hi {clientName},

        A viewing has been scheduled for {propertyTitle}.
        Please check your conversation for further details from the agent.

        View enquiry: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(clientEmail, subject, html, clientName, text) { IncludeUnsubscribe = false };
    }

    public static EmailMessage ViewingScheduledToAgent(
        string agentEmail,
        string agentName,
        string clientName,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"Viewing scheduled for {propertyTitle}";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Viewing Scheduled</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p>A viewing has been scheduled with <strong>{Esc(clientName)}</strong> for <strong>{Esc(propertyTitle)}</strong>.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Viewing Scheduled

        Hi {agentName},

        A viewing has been scheduled with {clientName} for {propertyTitle}.

        View enquiry: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(agentEmail, subject, html, agentName, text) { IncludeUnsubscribe = false };
    }

    // ── 5. Enquiry resolved → Client ─────────────────────────────────────

    public static EmailMessage EnquiryResolvedToClient(
        string clientEmail,
        string clientName,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"Your enquiry about {propertyTitle} has been resolved";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Enquiry Resolved</h2>
          <p>Hi <strong>{Esc(clientName)}</strong>,</p>
          <p>Your enquiry about <strong>{Esc(propertyTitle)}</strong> has been marked as resolved.</p>
          <p>If you have any further questions, feel free to submit a new enquiry.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Enquiry Resolved

        Hi {clientName},

        Your enquiry about {propertyTitle} has been marked as resolved.
        If you have any further questions, feel free to submit a new enquiry.

        View enquiry: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(clientEmail, subject, html, clientName, text) { IncludeUnsubscribe = false };
    }

    // ── 6. Admin notifies Agent ──────────────────────────────────────────

    public static EmailMessage AdminNotifyToAgent(
        string agentEmail,
        string agentName,
        string clientName,
        string propertyTitle,
        int enquiryId,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/enquiries/{enquiryId}";
        var subject = $"Enquiry from {clientName} about {propertyTitle} needs your attention";
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Enquiry Reminder</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p>An admin has flagged the enquiry from <strong>{Esc(clientName)}</strong> regarding <strong>{Esc(propertyTitle)}</strong> for your attention.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View &amp; Reply to Enquiry</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Enquiry Reminder

        Hi {agentName},

        An admin has flagged the enquiry from {clientName} regarding {propertyTitle} for your attention.

        View and reply: {ctaUrl}

        ---
        This is an automated notification from {BrandName}.
        """;

        return new EmailMessage(agentEmail, subject, html, agentName, text) { IncludeUnsubscribe = false };
    }

    // ── Helper ───────────────────────────────────────────────────────────

    // ── 7. Email verification code → User ──────────────────────────────

    public static EmailMessage EmailVerification(
        string recipientEmail,
        string recipientName,
        string code,
        int expiryMinutes,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/verify-email";
        var subject = "Verify your PIPDC account";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Verify your email</h2>
          <p>Hi <strong>{Esc(recipientName)}</strong>,</p>
          <p>Enter the 6-digit code below to confirm your PIPDC account.</p>
          <div style="margin:20px 0;padding:16px;background:#f0f7f4;border:1px dashed #1a5276;border-radius:8px;font-size:28px;font-weight:bold;letter-spacing:8px;text-align:center;color:#1a5276">{code}</div>
          <p style="font-size:13px;color:#666">This code expires in {expiryMinutes} minutes. If you did not create an account, you can safely ignore this email.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:8px 0">Enter my code</a>
          <p style="font-size:12px;color:#888;margin-top:20px">This is an automated security message from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Verify your email

        Hi {recipientName},

        Enter the 6-digit code below to confirm your PIPDC account.

        Your code: {code}

        This code expires in {expiryMinutes} minutes.
        If you did not create an account, you can safely ignore this email.

        Enter your code here: {ctaUrl}

        ---
        This is an automated security message from {BrandName}.
        """;

        return new EmailMessage(recipientEmail, subject, html, recipientName, text) { IncludeUnsubscribe = false };
    }

    // ── 8. Password reset code → User ──────────────────────────────────

    public static EmailMessage PasswordReset(
        string recipientEmail,
        string recipientName,
        string code,
        int expiryMinutes,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/reset-password";
        var subject = "Reset your PIPDC password";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Reset your password</h2>
          <p>Hi <strong>{Esc(recipientName)}</strong>,</p>
          <p>We received a request to reset your PIPDC password. Enter the 6-digit code below, then choose a new password.</p>
          <div style="margin:20px 0;padding:16px;background:#f0f7f4;border:1px dashed #1a5276;border-radius:8px;font-size:28px;font-weight:bold;letter-spacing:8px;text-align:center;color:#1a5276">{code}</div>
          <p style="font-size:13px;color:#666">This code expires in {expiryMinutes} minutes. If you did not request a reset, you can safely ignore this email.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:8px 0">Reset my password</a>
          <p style="font-size:12px;color:#888;margin-top:20px">This is an automated security message from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Reset your password

        Hi {recipientName},

        We received a request to reset your PIPDC password. Enter the 6-digit code below, then choose a new password.

        Your code: {code}

        This code expires in {expiryMinutes} minutes.
        If you did not request a reset, you can safely ignore this email.

        Reset your password here: {ctaUrl}

        ---
        This is an automated security message from {BrandName}.
        """;

        return new EmailMessage(recipientEmail, subject, html, recipientName, text) { IncludeUnsubscribe = false };
    }

    // ── 9. Password changed notification → User ─────────────────────────

    public static EmailMessage PasswordChangedNotification(
        string recipientEmail,
        string recipientName,
        string baseUrl)
    {
        var ctaUrl = $"{baseUrl}/forgot-password";
        var subject = "Your PIPDC password was changed";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Your password was changed</h2>
          <p>Hi <strong>{Esc(recipientName)}</strong>,</p>
          <p>Your PIPDC account password was changed. If this was you, no further action is needed.</p>
          <p style="font-size:13px;color:#666">If you did NOT make this change, secure your account immediately by resetting your password.</p>
          <a href="{ctaUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:8px 0">Reset my password</a>
          <p style="font-size:12px;color:#888;margin-top:20px">This is an automated security message from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your password was changed

        Hi {recipientName},

        Your PIPDC account password was changed. If this was you, no further action is needed.

        If you did NOT make this change, secure your account immediately:
        {ctaUrl}

        ---
        This is an automated security message from {BrandName}.
        """;

        return new EmailMessage(recipientEmail, subject, html, recipientName, text) { IncludeUnsubscribe = false };
    }

    // ── 11. Public contact form submission → configured recipient ────────

    /// <summary>
    /// Builds the notification email for a public contact-form submission.
    /// </summary>
    /// <remarks>
    /// The visitor's address is set as <c>ReplyTo</c> rather than <c>To</c>, so
    /// replying to the notification reaches the visitor directly. The visitor's
    /// own <c>Subject</c> is free text and lands in an email subject line, so it
    /// is HTML-encoded and length-limited here to keep header injection and
    /// unbounded headers out of the message.
    /// </remarks>
    public static EmailMessage ContactFormMessage(
        string recipientEmail,
        string senderName,
        string senderEmail,
        string? senderPhone,
        string subject,
        string message)
    {
        // Strip CR/LF so a crafted subject cannot inject extra headers.
        var safeSubject = Esc(subject.Trim());
        if (safeSubject.Length > 120) safeSubject = safeSubject[..120];

        var emailSubject = $"[Contact form] {safeSubject}";

        var phoneRow = string.IsNullOrWhiteSpace(senderPhone)
            ? string.Empty
            : $"<tr><td style=\"padding:4px 12px 4px 0;color:#888;white-space:nowrap\">Phone</td>" +
              $"<td style=\"padding:4px 0\">{Esc(senderPhone)}</td></tr>";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
        <h2 style="color:#1a5276">New contact form submission</h2>
        <p style="font-size:13px;color:#666">Reply directly to this email to respond to the sender.</p>
        <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
        <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Name</td><td style="padding:4px 0">{Esc(senderName)}</td></tr>
        <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Email</td><td style="padding:4px 0">{Esc(senderEmail)}</td></tr>
        {phoneRow}
        <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Subject</td><td style="padding:4px 0">{Esc(subject)}</td></tr>
        </table>
        <div style="margin:16px 0;padding:16px;background:#f0f7f4;border-left:4px solid #1a5276;border-radius:4px;white-space:pre-wrap">{Esc(message)}</div>
        <p style="font-size:12px;color:#888;margin-top:20px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var phoneText = string.IsNullOrWhiteSpace(senderPhone) ? string.Empty : $"Phone: {senderPhone.Trim()}\n";

        var text = $"""
        New contact form submission

        Reply directly to this email to respond to the sender.

        Name: {senderName.Trim()}
        Email: {senderEmail.Trim()}
        {phoneText}Subject: {subject.Trim()}

        Message:
        {message.Trim()}
        """;

        return new EmailMessage(recipientEmail, emailSubject, html, senderName, text)
        {
            IncludeUnsubscribe = false,
            ReplyTo = senderEmail
        };
    }

    // ── 12. Agent application approved ───────────────────────────────────

    public static EmailMessage AgentApplicationApproved(
        string applicantEmail,
        string applicantName,
        string licenseNumber,
        string baseUrl)
    {
        var dashboardUrl = $"{baseUrl}/dashboard";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Your agent application has been approved</h2>
          <p>Hi <strong>{Esc(applicantName)}</strong>,</p>
          <p>Your application to become a {BrandName} agent has been approved. You can now sign in and start listing properties.</p>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Licence number</td>
                <td style="padding:4px 0"><strong>{Esc(licenseNumber)}</strong></td></tr>
          </table>
          <p style="font-size:14px;color:#555">Keep this number safe. It identifies your agency on every listing you publish.</p>
          <a href="{dashboardUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Go to your dashboard</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your agent application has been approved

        Hi {applicantName},

        Your application to become a {BrandName} agent has been approved. You can now sign in and start listing properties.

        Licence number: {licenseNumber}

        Keep this number safe. It identifies your agency on every listing you publish.

        Sign in: {dashboardUrl}
        """;

        return new EmailMessage(applicantEmail, "Your agent application has been approved", html, applicantName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 13. Agent application rejected ───────────────────────────────────

    public static EmailMessage AgentApplicationRejected(
        string applicantEmail,
        string applicantName,
        string reason,
        string baseUrl)
    {
        var reapplyUrl = $"{baseUrl}/apply-agent";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Your agent application was not approved</h2>
          <p>Hi <strong>{Esc(applicantName)}</strong>,</p>
          <p>Thank you for applying to become a {BrandName} agent. We were not able to approve your application at this time.</p>
          <div style="margin:16px 0;padding:16px;background:#fdf3f3;border-left:4px solid #b03a3a;border-radius:4px;white-space:pre-wrap">{Esc(reason)}</div>
          <p style="font-size:14px;color:#555">You are welcome to apply again once the reason above has been addressed.</p>
          <a href="{reapplyUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Apply again</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your agent application was not approved

        Hi {applicantName},

        Thank you for applying to become a {BrandName} agent. We were not able to approve your application at this time.

        Reason:
        {reason}

        You are welcome to apply again once the reason above has been addressed.

        Apply again: {reapplyUrl}
        """;

        return new EmailMessage(applicantEmail, "Your agent application was not approved", html, applicantName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 14. Agent application received (applicant acknowledgement) ───────

    public static EmailMessage AgentApplicationReceived(
        string applicantEmail,
        string applicantName,
        string baseUrl)
    {
        var applicationUrl = $"{baseUrl}/dashboard/agent-application";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">We received your agent application</h2>
          <p>Hi <strong>{Esc(applicantName)}</strong>,</p>
          <p>Thank you for applying to become a {BrandName} agent. Your application has been received and is now
             with our review team.</p>
          <p>Every application is assessed by a person, so this takes a little time. We will email you as soon as
             there is a decision.</p>
          <p style="font-size:14px;color:#555">You can check the status of your application at any time from your
             dashboard. You do not need to submit a second application.</p>
          <a href="{applicationUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View my application</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        We received your agent application

        Hi {applicantName},

        Thank you for applying to become a {BrandName} agent. Your application has been received and is now with our review team.

        Every application is assessed by a person, so this takes a little time. We will email you as soon as there is a decision.

        You can check the status of your application at any time from your dashboard. You do not need to submit a second application.

        View my application: {applicationUrl}
        """;

        return new EmailMessage(applicantEmail, "We received your agent application", html, applicantName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 15. New agent application (admin notification) ───────────────────

    public static EmailMessage NewAgentApplicationToAdmin(
        string recipientEmail,
        string applicantName,
        string applicantEmail,
        string applicantPhone,
        string stateOfOrigin,
        string localGovernmentArea,
        int yearsOfExperience,
        DateTime createdAtUtc,
        string baseUrl)
    {
        var reviewUrl = $"{baseUrl}/dashboard/applications";
        var submittedOn = createdAtUtc.ToString("d MMMM yyyy 'at' HH:mm 'UTC'");

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">New agent application</h2>
          <p><strong>{Esc(applicantName)}</strong> has applied to become a {BrandName} agent and is awaiting review.</p>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Applicant</td>
                <td style="padding:4px 0">{Esc(applicantName)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Account email</td>
                <td style="padding:4px 0">{Esc(applicantEmail)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Phone</td>
                <td style="padding:4px 0">{Esc(applicantPhone)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">State of origin</td>
                <td style="padding:4px 0">{Esc(stateOfOrigin)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Local government area</td>
                <td style="padding:4px 0">{Esc(localGovernmentArea)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Experience</td>
                <td style="padding:4px 0">{yearsOfExperience} year(s)</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Submitted</td>
                <td style="padding:4px 0">{Esc(submittedOn)}</td></tr>
          </table>
          <p style="font-size:14px;color:#555">The applicant's full details, including their identification number,
             are in the review queue. It is deliberately not included in this email.</p>
          <a href="{reviewUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Open the review queue</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        New agent application

        {applicantName} has applied to become a {BrandName} agent and is awaiting review.

        Applicant: {applicantName}
        Account email: {applicantEmail}
        Phone: {applicantPhone}
        State of origin: {stateOfOrigin}
        Local government area: {localGovernmentArea}
        Experience: {yearsOfExperience} year(s)
        Submitted: {submittedOn}

        The applicant's full details, including their identification number, are in the review queue. It is deliberately not included in this email.

        Open the review queue: {reviewUrl}
        """;

        return new EmailMessage(recipientEmail, $"New agent application: {applicantName}", html, "Review Team", text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 15. Agent registration removed ───────────────────────────────────

    public static EmailMessage AgentRegistrationRemoved(
        string agentEmail,
        string agentName,
        string reason,
        bool listingsReassigned,
        string baseUrl)
    {
        var appealUrl = $"{baseUrl}/dashboard/agent-application";

        var reassignment = listingsReassigned
            ? """
              <p>Your property listings and any open enquiries have been transferred to another
                 agent, so clients can continue to make progress on them.</p>
              """
            : """
              <p>Your property listings and any open enquiries remain assigned to you and are
                 currently hidden from the public site. Contact us if you need them reviewed
                 or released.</p>
              """;

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#b03a3a">Your agent registration has been removed</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p>An administrator has removed your {BrandName} agent registration. You can no longer
             act as an agent on {BrandName}, and your agent dashboard is no longer available to you.</p>
          <div style="margin:16px 0;padding:16px;background:#fdf3f3;border-left:4px solid #b03a3a;border-radius:4px;white-space:pre-wrap"><strong>Reason</strong><br />{Esc(reason)}</div>
          {reassignment}
          <p style="font-size:14px;color:#555">Your account is still active, and you can continue to use
             {BrandName} as a regular user.</p>
          <a href="{appealUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Appeal this decision</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your agent registration has been removed

        Hi {agentName},

        An administrator has removed your {BrandName} agent registration. You can no longer act as an agent on {BrandName}, and your agent dashboard is no longer available to you.

        Reason:
        {reason}

        {(listingsReassigned
            ? "Your property listings and any open enquiries have been transferred to another agent, so clients can continue to make progress on them."
            : "Your property listings and any open enquiries remain assigned to you and are currently hidden from the public site. Contact us if you need them reviewed or released.")}

        Your account is still active, and you can continue to use {BrandName} as a regular user.

        Appeal this decision: {appealUrl}
        """;

        return new EmailMessage(agentEmail, "Your agent registration has been removed", html, agentName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 16. Agent appeal lodged (to admin) ───────────────────────────────

    public static EmailMessage AgentAppealLodgedToAdmin(
        string recipientEmail,
        string agentName,
        string agentEmailAddress,
        string reason,
        DateTime lodgedOn,
        string baseUrl)
    {
        var appealUrl = $"{baseUrl}/dashboard/agents";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">A removed agent has appealed</h2>
          <p><strong>{Esc(agentName)}</strong> has appealed the removal of their agent registration
             and is awaiting a decision.</p>
          <div style="margin:16px 0;padding:16px;background:#f4f6f7;border-left:4px solid #1a5276;border-radius:4px;white-space:pre-wrap">{Esc(reason)}</div>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Account email</td>
                <td style="padding:4px 0">{Esc(agentEmailAddress)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Lodged</td>
                <td style="padding:4px 0">{Esc(lodgedOn.ToString("u"))}</td></tr>
          </table>
          <a href="{appealUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Review the appeal</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        A removed agent has appealed

        {agentName} has appealed the removal of their agent registration and is awaiting a decision.

        {reason}

        Account email: {agentEmailAddress}
        Lodged: {lodgedOn:u}

        Review the appeal: {appealUrl}
        """;

        return new EmailMessage(recipientEmail, $"Agent appeal from {agentName}", html, "Review Team", text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 17. Agent appeal upheld ──────────────────────────────────────────

    public static EmailMessage AgentAppealUpheld(
        string agentEmailAddress,
        string agentName,
        string licenseNumber,
        string baseUrl)
    {
        var dashboardUrl = $"{baseUrl}/dashboard";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Your appeal was successful</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p>An administrator has reviewed your appeal against the removal of your agent
             registration and has decided in your favour.</p>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Licence number</td>
                <td style="padding:4px 0"><strong>{Esc(licenseNumber)}</strong></td></tr>
          </table>
          <p>Your agent access has been restored and your agent dashboard is available again.</p>
          <a href="{dashboardUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Go to your dashboard</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your appeal was successful

        Hi {agentName},

        An administrator has reviewed your appeal against the removal of your agent registration and has decided in your favour.

        Licence number: {licenseNumber}

        Your agent access has been restored and your agent dashboard is available again.

        Sign in: {dashboardUrl}
        """;

        return new EmailMessage(agentEmailAddress, "Your agent appeal was successful", html, agentName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 18. Agent appeal refused ─────────────────────────────────────────

    public static EmailMessage AgentAppealRefused(
        string agentEmailAddress,
        string agentName,
        string decisionNote,
        bool mayReapply,
        string baseUrl)
    {
        var applyUrl = $"{baseUrl}/apply-agent";

        var nextStep = mayReapply
            ? $"""
               <p style="font-size:14px;color:#555">You may submit a fresh application. Doing so starts a
                  new review; it does not reinstate the registration that was removed.</p>
               <a href="{applyUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Submit a new application</a>
               """
            : """
               <p style="font-size:14px;color:#555">You are not currently able to submit a further
                  application. If you believe this is an error, contact the {BrandName} team.</p>
               """;

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#b03a3a">Your appeal was not successful</h2>
          <p>Hi <strong>{Esc(agentName)}</strong>,</p>
          <p>An administrator has reviewed your appeal against the removal of your agent
             registration and has decided to uphold the original decision.</p>
          <div style="margin:16px 0;padding:16px;background:#fdf3f3;border-left:4px solid #b03a3a;border-radius:4px;white-space:pre-wrap">{Esc(decisionNote)}</div>
          {nextStep}
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your appeal was not successful

        Hi {agentName},

        An administrator has reviewed your appeal against the removal of your agent registration and has decided to uphold the original decision.

        {decisionNote}

        {(mayReapply
            ? "You may submit a fresh application. Doing so starts a new review; it does not reinstate the registration that was removed.\n\nSubmit a new application: " + applyUrl
            : $"You are not currently able to submit a further application. If you believe this is an error, contact the {BrandName} team.")}
        """;

        return new EmailMessage(agentEmailAddress, "Your agent appeal was not successful", html, agentName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 19. Blocked from applying ────────────────────────────────────────

    public static EmailMessage AgentApplicationBlocked(
        string applicantEmail,
        string applicantName,
        string reason,
        string baseUrl)
    {
        var contactUrl = $"{baseUrl}/contact";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#b03a3a">You can no longer apply to become a {BrandName} agent</h2>
          <p>Hi <strong>{Esc(applicantName)}</strong>,</p>
          <p>An administrator has decided that your account is not permitted to apply for an agent
             registration. The application form is no longer available to you.</p>
          <div style="margin:16px 0;padding:16px;background:#fdf3f3;border-left:4px solid #b03a3a;border-radius:4px;white-space:pre-wrap">{Esc(reason)}</div>
          <p style="font-size:14px;color:#555">This decision can be reviewed. If you believe it is a mistake,
             contact the {BrandName} team.</p>
          <a href="{contactUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Contact {BrandName}</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        You can no longer apply to become a {BrandName} agent

        Hi {applicantName},

        An administrator has decided that your account is not permitted to apply for an agent registration. The application form is no longer available to you.

        Reason:
        {reason}

        This decision can be reviewed. If you believe it is a mistake, contact the {BrandName} team.

        Contact us: {contactUrl}
        """;

        return new EmailMessage(applicantEmail, $"You can no longer apply to become a {BrandName} agent", html, applicantName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── 20. Block lifted ─────────────────────────────────────────────────

    public static EmailMessage AgentApplicationBlockLifted(
        string applicantEmail,
        string applicantName,
        string baseUrl)
    {
        var applyUrl = $"{baseUrl}/apply-agent";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">You can apply to become a {BrandName} agent again</h2>
          <p>Hi <strong>{Esc(applicantName)}</strong>,</p>
          <p>An administrator has lifted the restriction on your account. The agent application
             form is available to you again.</p>
          <a href="{applyUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Apply to become an agent</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        You can apply to become a {BrandName} agent again

        Hi {applicantName},

        An administrator has lifted the restriction on your account. The agent application form is available to you again.

        Apply now: {applyUrl}
        """;

        return new EmailMessage(applicantEmail, $"You can apply to become a {BrandName} agent again", html, applicantName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── Helper ───────────────────────────────────────────────────────────

    // ---- Batch 6: concierge-style escalation of a client conversation to PIPDC ----

    public static EmailMessage ConversationEscalatedToAdmin(
        string recipientEmail,
        string clientName,
        string agentName,
        string propertyTitle,
        string escalationReason,
        string baseUrl)
    {
        var queueUrl = $"{baseUrl}/dashboard/escalations";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">A client conversation was escalated</h2>
          <p><strong>{Esc(agentName)}</strong> handed a conversation to {BrandName} for help.</p>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Client</td>
                <td style="padding:4px 0">{Esc(clientName)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Handled by</td>
                <td style="padding:4px 0">{Esc(agentName)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Property</td>
                <td style="padding:4px 0">{Esc(propertyTitle)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Reason</td>
                <td style="padding:4px 0">{Esc(escalationReason)}</td></tr>
          </table>
          <a href="{queueUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Open the escalation queue</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        A client conversation was escalated

        {agentName} handed a conversation to {BrandName} for help.

        Client: {clientName}
        Handled by: {agentName}
        Property: {propertyTitle}
        Reason: {escalationReason}

        Open the escalation queue: {queueUrl}
        """;

        return new EmailMessage(recipientEmail, $"Escalated conversation: {clientName}", html, "Review Team", text)
        {
            IncludeUnsubscribe = false
        };
    }

    public static EmailMessage ConciergeEscalatedToAdmin(
        string recipientEmail,
        string clientName,
        string escalationReason,
        string baseUrl)
    {
        var queueUrl = $"{baseUrl}/dashboard/escalations";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">A concierge chat needs your help</h2>
          <p>The AI property assistant referred a client to the {BrandName} team because it could not resolve the request itself.</p>
          <table style="margin:16px 0;font-size:14px;border-collapse:collapse">
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Client</td>
                <td style="padding:4px 0">{Esc(clientName)}</td></tr>
            <tr><td style="padding:4px 12px 4px 0;color:#888;white-space:nowrap">Reason</td>
                <td style="padding:4px 0">{Esc(escalationReason)}</td></tr>
          </table>
          <p style="font-size:13px;color:#555">Open the case to read the full chat transcript before reaching out to the client.</p>
          <a href="{queueUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">Open the escalation queue</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        A concierge chat needs your help

        The AI property assistant referred a client to {BrandName} because it could not resolve the request itself.

        Client: {clientName}
        Reason: {escalationReason}

        Open the case to read the full chat transcript before reaching out to the client.

        Open the escalation queue: {queueUrl}
        """;

        return new EmailMessage(recipientEmail, $"A concierge chat needs help: {clientName}", html, "Review Team", text)
        {
            IncludeUnsubscribe = false
        };
    }

    public static EmailMessage ConversationEscalatedToClient(
        string clientEmail,
        string clientName,
        string agentName,
        string propertyTitle,
        string baseUrl)
    {
        var messagesUrl = $"{baseUrl}/messages";

        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">We are looking into your enquiry</h2>
          <p>Hi {Esc(clientName)},</p>
          <p>Your agent <strong>{Esc(agentName)}</strong> has passed your conversation about
             &ldquo;{Esc(propertyTitle)}&rdquo; to the {BrandName} team, who will take it from here.</p>
          <p>You do not need to do anything. You can still reply in the conversation, and the full
             history stays with you.</p>
          <a href="{messagesUrl}" style="display:inline-block;padding:12px 24px;background:#1a5276;color:#fff;text-decoration:none;border-radius:4px;margin:16px 0">View your messages</a>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        We are looking into your enquiry

        Hi {clientName},

        Your agent {agentName} has passed your conversation about "{propertyTitle}" to the {BrandName} team, who will take it from here.

        You do not need to do anything. You can still reply in the conversation, and the full history stays with you.

        View your messages: {messagesUrl}
        """;

        return new EmailMessage(clientEmail, $"We are looking into your enquiry about {propertyTitle}", html, clientName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    public static EmailMessage ConversationAssignedToAdmin(
        string clientEmail,
        string clientName,
        string adminName,
        string propertyTitle)
    {
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">An administrator is now handling your enquiry</h2>
          <p>Hi {Esc(clientName)},</p>
          <p><strong>{Esc(adminName)}</strong> from {BrandName} has taken over your conversation about
             &ldquo;{Esc(propertyTitle)}&rdquo; and will reply to you here.</p>
          <p>Your original agent stays copied on the conversation, so nothing is lost.</p>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        An administrator is now handling your enquiry

        Hi {clientName},

        {adminName} from {BrandName} has taken over your conversation about "{propertyTitle}" and will reply to you here.

        Your original agent stays copied on the conversation, so nothing is lost.
        """;

        return new EmailMessage(clientEmail, $"An administrator is handling your enquiry about {propertyTitle}", html, clientName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    public static EmailMessage ConversationEscalationResolved(
        string clientEmail,
        string clientName,
        string adminName,
        string propertyTitle)
    {
        var html = $"""
        <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;color:#333">
          <h2 style="color:#1a5276">Your enquiry has been resolved</h2>
          <p>Hi {Esc(clientName)},</p>
          <p><strong>{Esc(adminName)}</strong> has marked your conversation about
             &ldquo;{Esc(propertyTitle)}&rdquo; as resolved.</p>
          <p>The conversation is closed to new replies, but you can still read the whole history.</p>
          <p style="font-size:12px;color:#888;margin-top:24px">This is an automated notification from {BrandName}.</p>
        </div>
        """;

        var text = $"""
        Your enquiry has been resolved

        Hi {clientName},

        {adminName} has marked your conversation about "{propertyTitle}" as resolved.

        The conversation is closed to new replies, but you can still read the whole history.
        """;

        return new EmailMessage(clientEmail, $"Resolved: your enquiry about {propertyTitle}", html, clientName, text)
        {
            IncludeUnsubscribe = false
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Esc(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
