namespace PIPDC.Application.Email;

/// <summary>
/// Factory methods that produce <see cref="EmailMessage"/> instances for
/// each notification event.  The application layer calls these; the
/// infrastructure layer (GmailApiEmailService) sends them.
/// </summary>
public static class EmailTemplates
{
    private const string BrandName = "PIPDC";

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

    // ── Helper ───────────────────────────────────────────────────────────

    private static string Esc(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
