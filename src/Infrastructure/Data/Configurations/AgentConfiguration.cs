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

        // Removal hides an agent from the public directory and the public listing
        // queries in the same way suspension does, so it is indexed for the same
        // reason.
        builder.HasIndex(a => a.IsRemoved);

        builder.Property(a => a.RemovalReason)
            .HasMaxLength(1000);

        builder.Property(a => a.RemovedByAdminId)
            .HasMaxLength(450);

        builder.HasOne(a => a.User)
            .WithOne(u => u.Agent)
            .HasForeignKey<Agent>(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Who decided to remove the agent. Restrict, so an administrator account
        // cannot be deleted out from under the record of the decision. The
        // navigation is configured explicitly because there is no convention that
        // pairs it with AppUser.AgentsRemoved, and an unconfigured navigation fails
        // the model at design time.
        builder.HasOne(a => a.RemovedByAdmin)
            .WithMany(u => u.AgentsRemoved)
            .HasForeignKey(a => a.RemovedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        // A revoked registration can be reinstated by an upheld appeal, so the
        // successor agent is Restrict: a replacement agent must never be deletable
        // out from under the record that points at it.
        builder.HasOne(a => a.ReassignedToAgent)
            .WithMany()
            .HasForeignKey(a => a.ReassignedToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
