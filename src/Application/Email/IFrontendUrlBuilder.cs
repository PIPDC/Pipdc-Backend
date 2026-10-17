namespace PIPDC.Application.Email;

/// <summary>
/// Builds absolute links to the frontend for the calls to action in notification
/// emails. Every email link goes through here, so a route change is made once,
/// in <see cref="FrontendRoutes"/>, rather than in each template.
/// </summary>
public interface IFrontendUrlBuilder
{
    /// <summary>The frontend home page.</summary>
    string Base { get; }

    string Dashboard();
    string Contact();
    string Developments();
    string ApplyAgent();
    string ForgotPassword();
    string MyAgentApplication();
    string ApplicationQueue();
    string AgentAppeals();
    string EscalationQueue();

    /// <summary>The signed-in user's conversations.</summary>
    string Messages();

    /// <summary>
    /// The conversations page focused on one enquiry. The messaging view reads
    /// the <c>enquiry</c> query parameter, so this deep-links to the enquiry's
    /// thread rather than a dedicated enquiry page (there is none).
    /// </summary>
    string EnquiryMessages(int enquiryId);

    /// <summary>The verification page, with the address pre-filled.</summary>
    string VerifyEmail(string email);

    /// <summary>The password-reset page, with the address pre-filled.</summary>
    string ResetPassword(string email);
}
