using Microsoft.AspNetCore.Identity;

namespace PIPDC.Domain.Entities;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Agent? Agent { get; set; }
    public ICollection<AgentApplication> AgentApplications { get; set; } = [];
    public ICollection<SavedProperty> SavedProperties { get; set; } = [];
    public ICollection<AiChatSession> AiChatSessions { get; set; } = [];
    public ICollection<Enquiry> Enquiries { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
