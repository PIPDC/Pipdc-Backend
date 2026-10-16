namespace PIPDC.Domain.Enums;

/// <summary>
/// The channel the client chose when the concierge handed their chat to PIPDC.
/// </summary>
public enum ConciergeContactPreference
{
    /// <summary>The client asked to be contacted by email only.</summary>
    Email = 0,

    /// <summary>The client asked to be contacted by email and phone.</summary>
    EmailAndPhone = 1
}
