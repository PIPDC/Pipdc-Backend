using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class ConciergeEscalationConfiguration : IEntityTypeConfiguration<ConciergeEscalation>
{
    public void Configure(EntityTypeBuilder<ConciergeEscalation> builder)
    {
        builder.Property(e => e.EscalationReason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.EscalatedAt)
            .IsRequired();

        // Escalation state is stored as a string so the column stays readable and
        // a future value can be added without renumbering the existing ones.
        builder.Property(e => e.EscalationStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ConciergeEscalationStatus.Escalated);

        builder.Property(e => e.AssignedAdminId).HasMaxLength(450);
        builder.Property(e => e.ResolvedByUserId).HasMaxLength(450);

        // The escalation lives for exactly as long as its transcript: deleting the
        // chat session removes the case along with the history.
        builder.HasOne(e => e.AiChatSession)
            .WithMany()
            .HasForeignKey(e => e.AiChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, matching the other actor columns: an administrator's account
        // must not be deletable while an escalation decision points at it.
        builder.HasOne(e => e.AssignedAdmin)
            .WithMany()
            .HasForeignKey(e => e.AssignedAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ResolvedByUser)
            .WithMany()
            .HasForeignKey(e => e.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EscalationStatus);
        builder.HasIndex(e => e.AssignedAdminId);
        builder.HasIndex(e => e.AiChatSessionId);
    }
}