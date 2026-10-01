using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentApplicationConfiguration : IEntityTypeConfiguration<AgentApplication>
{
    public void Configure(EntityTypeBuilder<AgentApplication> builder)
    {
        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.StateOfOrigin)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.ResidentialAddress)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.LocalGovernmentArea)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.AgencyName)
            .HasMaxLength(200);

        builder.Property(a => a.AdditionalNotes)
            .HasMaxLength(2000);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.ReviewedByAdminId)
            .HasMaxLength(450);

        builder.Property(a => a.RejectionReason)
            .HasMaxLength(1000);

        builder.HasIndex(a => a.Status);

        // One user may hold at most one open application at a time. A user can
        // still reapply after a rejection. PostgreSQL partial index:
        //   CREATE UNIQUE INDEX ... ON "AgentApplications" ("UserId")
        //   WHERE "Status" IN ('Submitted', 'UnderReview');
        builder.HasIndex(a => a.UserId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Submitted', 'UnderReview')")
            .HasDatabaseName("IX_AgentApplications_UserId_Open");

        builder.HasOne(a => a.User)
            .WithMany(u => u.AgentApplications)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
