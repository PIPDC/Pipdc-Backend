namespace PIPDC.Application.Email;

/// <summary>
/// Frontend route paths used as the destination of email calls to action.
/// </summary>
/// <remarks>
/// This is the single source of truth for every link an email points at. Each
/// path is kept in step with the route table in the frontend router
/// (PIPDC-Frontend, src/App.tsx). If a route moves there, it moves here; a link
/// built from a stale path lands on the not-found page, which is exactly the
/// bug these constants exist to prevent.
/// </remarks>
public static class FrontendRoutes
{
    public const string Home = "/";
    public const string Contact = "/contact";
    public const string Developments = "/developments";
    public const string ApplyAgent = "/apply-agent";
    public const string ForgotPassword = "/forgot-password";
    public const string VerifyEmail = "/verify-email";
    public const string ResetPassword = "/reset-password";

    public const string Dashboard = "/dashboard";
    public const string Messages = "/dashboard/messages";
    public const string MyAgentApplication = "/dashboard/my-agent-application";
    public const string ApplicationQueue = "/dashboard/applications";
    public const string AgentAppeals = "/dashboard/agents";
    public const string EscalationQueue = "/dashboard/escalations";
}
