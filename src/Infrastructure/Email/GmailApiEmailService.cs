using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PIPDC.Application.Email;

namespace PIPDC.Infrastructure.Email;

public class GmailApiEmailService(
    IOptions<GmailApiSettings> options,
    ILogger<GmailApiEmailService> logger) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var settings = options.Value;

        try
        {
            var gmail = CreateGmailService(settings);

            var mimeMessage = new MimeMessage
            {
                Subject = message.Subject
            };

            mimeMessage.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            mimeMessage.To.Add(new MailboxAddress(message.ToName, message.To));

            if (!string.IsNullOrWhiteSpace(message.ReplyTo))
                mimeMessage.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));

            if (message.IncludeUnsubscribe)
            {
                mimeMessage.Headers.Add("List-Unsubscribe", $"<mailto:{settings.SenderEmail}?subject=Unsubscribe>");
                mimeMessage.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
            }

            var body = new BodyBuilder { HtmlBody = message.HtmlBody };

            if (!string.IsNullOrWhiteSpace(message.TextBody))
                body.TextBody = message.TextBody;

            mimeMessage.Body = body.ToMessageBody();

            var raw = Base64UrlEncode(mimeMessage);

            var sendRequest = new Message { Raw = raw };
            await gmail.Users.Messages.Send(sendRequest, settings.SenderEmail).ExecuteAsync(ct);

            logger.LogInformation(
                "Email sent to {Recipient} (subject: {Subject}) via Gmail API.",
                message.To, message.Subject);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Gmail API send failed for recipient {Recipient} (subject: {Subject}).",
                message.To, message.Subject);
            throw;
        }
    }

    private static GmailService CreateGmailService(GmailApiSettings settings)
    {
        var json = $$"""
        {
          "type": "authorized_user",
          "client_id": "{{settings.ClientId}}",
          "client_secret": "{{settings.ClientSecret}}",
          "refresh_token": "{{settings.RefreshToken}}"
        }
        """;

        var credential = GoogleCredential
            .FromJson(json)
            .CreateScoped(GmailService.Scope.GmailSend);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "PIPDC"
        });
    }

    private static string Base64UrlEncode(MimeMessage message)
    {
        using var stream = new MemoryStream();
        message.WriteTo(stream);
        return Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
