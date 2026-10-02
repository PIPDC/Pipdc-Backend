using Microsoft.AspNetCore.Identity;

namespace PIPDC.Domain.Entities;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // =========================
    // Location
    // =========================

    // Batch 5: where the user is, so "Properties Near You" has an honest reference
    // point. This points at the existing Location hierarchy rather than duplicating
    // it, and it is opt-in: a user with no location here simply has no nearby
    // section. Latitude/Longitude are null unless the user chose to share them.
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Agent? Agent { get; set; }
    public ICollection<AgentApplication> AgentApplications { get; set; } = [];
    public ICollection<AgentReport> SubmittedAgentReports { get; set; } = [];
    public ICollection<AgentReport> TriagedAgentReports { get; set; } = [];
    public ICollection<AgentReview> AgentReviews { get; set; } = [];
    public ICollection<Agent> AgentsSuspended { get; set; } = [];
    public ICollection<Agent> AgentsRemoved { get; set; } = [];
    public ICollection<SavedProperty> SavedProperties { get; set; } = [];
    public ICollection<AiChatSession> AiChatSessions { get; set; } = [];
    public ICollection<Enquiry> Enquiries { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
