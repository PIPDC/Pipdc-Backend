namespace PIPDC.Application.Email;

/// <summary>
/// Non-secret email configuration. Bound from the "Email" configuration section.
///
/// Anything secret (client secret, refresh token) stays in
/// <see cref="GmailApiSettings"/> / user secrets. This section only holds
/// non-secret values that are safe to commit, and which vary per environment.
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>
    /// Business address that receives public contact-form submissions and general
    /// enquiries. Overridable per environment, so production never has to reuse
    /// a development address.
    /// </summary>
    public string ContactRecipient { get; set; } = string.Empty;

    /// <summary>
    /// Address notified when a new agent application is submitted, so the review
    /// queue does not go unnoticed until someone happens to open the dashboard.
    /// Falls back to <see cref="ContactRecipient"/> when empty, so a deployment
    /// that only configures the contact address still notifies someone.
    /// </summary>
    public string AgentApplicationsRecipient { get; set; } = string.Empty;

    /// <summary>The address to notify about new agent applications.</summary>
    public string ResolveAgentApplicationsRecipient() =>
        string.IsNullOrWhiteSpace(AgentApplicationsRecipient) ? ContactRecipient : AgentApplicationsRecipient;
}
