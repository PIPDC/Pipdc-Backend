using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.Property(a => a.Bio)
            .HasMaxLength(4000);

        builder.Property(a => a.Title)
            .HasMaxLength(100);

        builder.Property(a => a.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(a => a.PhotoPublicId)
            .HasMaxLength(200);

        builder.Property(a => a.AgencyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.LicenseNumber)
            .HasMaxLength(100);

        // A licence number identifies exactly one agent, so it must be unique.
        // The index is the real guarantee: it is what makes two concurrent
        // approvals collide loudly on insert instead of silently issuing the same
        // licence twice. The generator also pre-checks, but that check alone
        // would be a race. Filtered to exclude NULL because existing agents
        // promoted before the licence workflow existed have no number, and
        // PostgreSQL already permits many NULLs in a unique index.
        builder.HasIndex(a => a.LicenseNumber)
            .IsUnique()
            .HasFilter("\"LicenseNumber\" IS NOT NULL")
            .HasDatabaseName("IX_Agents_LicenseNumber");

        builder.Property(a => a.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.SuspensionReason)
            .HasMaxLength(1000);

        // Public agent directory and public property listings both filter on this
        // flag, so it is indexed. The table is small, but the flag is on the
        // hottest read path in the application.
        builder.HasIndex(a => a.IsSuspended);

        builder.HasOne(a => a.User)
            .WithOne(u => u.Agent)
            .HasForeignKey<Agent>(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
