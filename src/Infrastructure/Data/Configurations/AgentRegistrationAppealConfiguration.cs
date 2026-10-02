using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentRegistrationAppealConfiguration : IEntityTypeConfiguration<AgentRegistrationAppeal>
{
    public void Configure(EntityTypeBuilder<AgentRegistrationAppeal> builder)
    {
        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.Reason)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(a => a.ReviewedByAdminId)
            .HasMaxLength(450);

        builder.Property(a => a.DecisionNote)
            .HasMaxLength(2000);

        // Stored as text, not an integer, because the partial index below filters
        // on the literal names. Without this conversion EF would write ints into an
        // index whose predicate compares against strings, and the predicate would
        // never match.
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(a => a.Status);

        // At most one appeal may be open at a time per account, so a double click
        // cannot produce two competing appeals an admin has to arbitrate between.
        // A refused or upheld appeal leaves the index, which is what permits a
        // fresh appeal later.
        builder.HasIndex(a => a.UserId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Submitted', 'UnderReview')")
            .HasDatabaseName("IX_AgentRegistrationAppeals_UserId_Open");

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull rather than Cascade: an administrator purging a revoked
        // application must not silently destroy the appeal that contested it. The
        // appeal is the record of the challenge and outlives the application.
        builder.HasOne(a => a.AgentApplication)
            .WithMany()
            .HasForeignKey(a => a.AgentApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
