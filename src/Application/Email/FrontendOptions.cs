namespace PIPDC.Application.Email;

/// <summary>
/// Public address of the frontend application. Bound from the "Frontend"
/// configuration section and used, via <see cref="IFrontendUrlBuilder"/>, to
/// build the links in notification emails.
/// </summary>
/// <remarks>
/// <see cref="GmailApiSettings.FrontendBaseUrl"/> is still read as a fallback so
/// an existing deployment that only sets that key keeps working. New
/// environments should set <c>Frontend:BaseUrl</c> (or the
/// <c>Frontend__BaseUrl</c> environment variable).
/// </remarks>
public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    /// <summary>
    /// Absolute base URL with no trailing slash, for example
    /// "https://pipdc.plateaustate.gov.ng". Left empty in source control; it is
    /// supplied per environment.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
