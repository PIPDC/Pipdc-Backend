using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentReviewConfiguration : IEntityTypeConfiguration<AgentReview>
{
    public void Configure(EntityTypeBuilder<AgentReview> builder)
    {
        builder.Property(r => r.ReviewerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.Comment)
            .HasMaxLength(1000);

        // A rating is a whole number from 1 to 5. The bound is checked in the
        // service so a caller gets a readable validation message rather than a
        // raw database constraint violation; this is a backstop only.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_AgentReviews_Rating_Range",
            "\"Rating\" >= 1 AND \"Rating\" <= 5"));

        // One review per reviewer per agent. A repeat submission updates the
        // existing row, so the public average cannot be inflated by one client
        // voting repeatedly.
        builder.HasIndex(r => new { r.AgentId, r.ReviewerUserId })
            .IsUnique()
            .HasDatabaseName("IX_AgentReviews_AgentId_ReviewerUserId");

        // Supports the aggregate the public agent projection reads.
        builder.HasIndex(r => r.AgentId);

        builder.HasOne(r => r.Agent)
            .WithMany(a => a.Reviews)
            .HasForeignKey(r => r.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reviewer)
            .WithMany(u => u.AgentReviews)
            .HasForeignKey(r => r.ReviewerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
