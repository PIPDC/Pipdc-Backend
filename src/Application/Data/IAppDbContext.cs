using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PIPDC.Domain.Auth;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Data;

public interface IAppDbContext
{
    DbSet<Property> Properties { get; }
    DbSet<PropertyImage> PropertyImages { get; }
    DbSet<Agent> Agents { get; }
    DbSet<AgentApplication> AgentApplications { get; }
    DbSet<AgentRegistrationAppeal> AgentRegistrationAppeals { get; }
    DbSet<AgentApplicationBlock> AgentApplicationBlocks { get; }
    DbSet<AgentReport> AgentReports { get; }
    DbSet<AgentReview> AgentReviews { get; }
    DbSet<Enquiry> Enquiries { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<SaleRecord> SaleRecords { get; }
    DbSet<LeaseRecord> LeaseRecords { get; }
    DbSet<BlogPost> BlogPosts { get; }
    DbSet<Category> Categories { get; }
    DbSet<Tag> Tags { get; }
    DbSet<BlogPostTag> BlogPostTags { get; }
    DbSet<Location> Locations { get; }
    DbSet<AppUser> Users { get; }
    DbSet<SavedProperty> SavedProperties { get; }
    DbSet<AiChatSession> AiChatSessions { get; }
    DbSet<ConciergeEscalation> ConciergeEscalations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<DevelopmentProject> DevelopmentProjects { get; }
    DbSet<DevelopmentUnit> DevelopmentUnits { get; }
    DbSet<DevelopmentUpdate> DevelopmentUpdates { get; }
    DbSet<DevelopmentProjectImage> DevelopmentProjectImages { get; }
    DbSet<DevelopmentTracking> DevelopmentTrackings { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<Notification> Notifications { get; }

    /// <summary>
    /// Exposed so a service that must make several writes land together can open a
    /// real transaction. Recording a sale or lease is one such case: the new record
    /// and the property's status change are only correct together.
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
