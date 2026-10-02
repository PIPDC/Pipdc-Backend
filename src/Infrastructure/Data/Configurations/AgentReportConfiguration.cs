using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentReportConfiguration : IEntityTypeConfiguration<AgentReport>
{
    public void Configure(EntityTypeBuilder<AgentReport> builder)
    {
        builder.Property(r => r.ReporterUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ReviewedByAdminId)
            .HasMaxLength(450);

        builder.Property(r => r.Reason)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.ResolutionNote)
            .HasMaxLength(1000);

        // The admin triage queue is read as "open reports, oldest first".
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        // Supports an agent's own report history in the admin agent view.
        builder.HasIndex(r => r.AgentId);
        builder.HasIndex(r => r.ReporterUserId);

        // Duplicate-report protection. The same client may keep an unresolved
        // report open against an agent, but cannot open a second one. Closed
        // reports fall outside the index, so a client can report again after a
        // dismissal. The service pre-checks for a readable message, but this
        // index is the authority: two concurrent submissions would both pass
        // the pre-check and this is what stops the duplicate. PostgreSQL
        // partial index, matching IX_AgentApplications_UserId_Open.
        builder.HasIndex(r => new { r.AgentId, r.ReporterUserId })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Open', 'UnderReview')")
            .HasDatabaseName("IX_AgentReports_AgentId_ReporterUserId_Open");

        builder.HasOne(r => r.Agent)
            .WithMany(a => a.Reports)
            .HasForeignKey(r => r.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Reporter)
            .WithMany(u => u.SubmittedAgentReports)
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Set to null rather than cascaded: deleting an administrator account
        // must not silently destroy the moderation record they created.
        builder.HasOne(r => r.ReviewedByAdmin)
            .WithMany(u => u.TriagedAgentReports)
            .HasForeignKey(r => r.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
