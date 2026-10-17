using Microsoft.Extensions.Options;
using PIPDC.Application.Email;

namespace PIPDC.Infrastructure.Email;

/// <summary>
/// Builds the frontend links used in notification emails from a configured base
/// URL.
/// </summary>
/// <remarks>
/// <para>
/// The base URL comes from <c>Frontend:BaseUrl</c>, falling back to the legacy
/// <c>GmailApiSettings:FrontendBaseUrl</c> key that older deployments set. A
/// trailing slash on either is trimmed so the built links never double up.
/// </para>
/// <para>
/// The builder is stateless after construction, so it is registered as a
/// singleton. It is deliberately tolerant of an empty base URL (the link is then
/// relative rather than absolute); <c>Program</c> fails the app at startup in
/// Production when the base URL is missing or malformed, so that condition never
/// reaches a live deployment unnoticed.
/// </para>
/// </remarks>
public sealed class FrontendUrlBuilder : IFrontendUrlBuilder
{
    private readonly string _baseUrl;

    public FrontendUrlBuilder(
        Microsoft.Extensions.Options.IOptions<FrontendOptions> frontendOptions,
        Microsoft.Extensions.Options.IOptions<GmailApiSettings> gmailOptions)
    {
        var configured = frontendOptions.Value.BaseUrl;

        if (string.IsNullOrWhiteSpace(configured))
            configured = gmailOptions.Value.FrontendBaseUrl;

        _baseUrl = string.IsNullOrWhiteSpace(configured)
            ? string.Empty
            : configured.Trim().TrimEnd('/');
    }

    public string Base => Link(FrontendRoutes.Home);

    public string Dashboard() => Link(FrontendRoutes.Dashboard);
    public string Contact() => Link(FrontendRoutes.Contact);
    public string Developments() => Link(FrontendRoutes.Developments);
    public string ApplyAgent() => Link(FrontendRoutes.ApplyAgent);
    public string ForgotPassword() => Link(FrontendRoutes.ForgotPassword);
    public string MyAgentApplication() => Link(FrontendRoutes.MyAgentApplication);
    public string ApplicationQueue() => Link(FrontendRoutes.ApplicationQueue);
    public string AgentAppeals() => Link(FrontendRoutes.AgentAppeals);
    public string EscalationQueue() => Link(FrontendRoutes.EscalationQueue);
    public string Messages() => Link(FrontendRoutes.Messages);

    public string EnquiryMessages(int enquiryId) =>
        $"{Link(FrontendRoutes.Messages)}?enquiry={enquiryId}";

    public string VerifyEmail(string email) =>
        $"{Link(FrontendRoutes.VerifyEmail)}?email={Uri.EscapeDataString(email)}";

    public string ResetPassword(string email) =>
        $"{Link(FrontendRoutes.ResetPassword)}?email={Uri.EscapeDataString(email)}";

    private string Link(string path) => $"{_baseUrl}{path}";
}
