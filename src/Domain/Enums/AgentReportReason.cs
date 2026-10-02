namespace PIPDC.Domain.Enums;

/// <summary>
/// Why a client is reporting an agent. Stored as a string so the categories stay
/// readable in the database and can be extended without a migration that
/// reinterprets existing rows.
/// </summary>
public enum AgentReportReason
{
    /// <summary>Misleading or fraudulent listing, fake property, or an attempt to obtain money dishonestly.</summary>
    FraudOrScam = 0,

    /// <summary>The listing does not exist, is not for sale or rent, or misrepresents the property.</summary>
    FalseListing = 1,

    /// <summary>Abusive, threatening or Persistent unwanted contact from the agent.</summary>
    Harassment = 2,

    /// <summary>Conduct that breaches professional or platform standards, but not covered by a more specific category.</summary>
    UnprofessionalConduct = 3,

    /// <summary>The property materially differs from what the agent advertised.</summary>
    PropertyNotAsAdvertised = 4,

    /// <summary>Practising as an agent without a valid PIPDC licence.</summary>
    UnauthorizedPractice = 5,

    /// <summary>Anything the other categories do not cover. Requires a written description.</summary>
    Other = 6
}
