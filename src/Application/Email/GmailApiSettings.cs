namespace PIPDC.Application.Email;

/// <summary>
/// OAuth2 credentials for the Gmail API.  Obtain these from
/// Google Cloud Console → APIs &amp; Services → Credentials
/// after enabling the Gmail API for your project.
/// </summary>
public sealed class GmailApiSettings
{
    /// <summary>
    /// OAuth2 client ID (type: Web application).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token obtained during the OAuth2 consent flow.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// The Gmail address to send from, e.g. "simonagboji2021@gmail.com".
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in the From header, e.g. "PIPDC".
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Public-facing base URL of the frontend application (no trailing slash).
    /// Used to build CTA links in notification emails, e.g. "https://pipdc-frontend.onrender.com".
    /// </summary>
    public string FrontendBaseUrl { get; set; } = string.Empty;
}
