using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.Property(c => c.ClientUserId)
            .IsRequired()
            .HasMaxLength(450);

        // One enquiry has at most one conversation. Enforced at the database level.
        builder.HasIndex(c => c.EnquiryId)
            .IsUnique();

        // Efficient lookup of a user's conversations and an agent's conversations.
        builder.HasIndex(c => c.ClientUserId);

        builder.HasIndex(c => c.AgentId);

        builder.HasOne(c => c.Enquiry)
            .WithOne(e => e.Conversation)
            .HasForeignKey<Conversation>(c => c.EnquiryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Client)
            .WithMany(u => u.Conversations)
            .HasForeignKey(c => c.ClientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Agent)
            .WithMany(a => a.Conversations)
            .HasForeignKey(c => c.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Escalation state is stored as a string so the column stays readable and
        // a future value can be added without renumbering the existing ones.
        builder.Property(c => c.EscalationStatus)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ConversationEscalationStatus.Active);

        builder.Property(c => c.EscalatedByUserId).HasMaxLength(450);
        builder.Property(c => c.EscalationReason).HasMaxLength(1000);
        builder.Property(c => c.AssignedAdminId).HasMaxLength(450);
        builder.Property(c => c.ResolvedByUserId).HasMaxLength(450);

        // The admin queue is a filtered scan over the non-active rows, and the
        // assigned admin's own queue filters on the admin id.
        builder.HasIndex(c => c.EscalationStatus);
        builder.HasIndex(c => c.AssignedAdminId);

        // Restrict, matching the other actor columns: an administrator's account
        // must not be deletable while an escalation decision points at it.
        builder.HasOne(c => c.EscalatedByUser)
            .WithMany()
            .HasForeignKey(c => c.EscalatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AssignedAdmin)
            .WithMany()
            .HasForeignKey(c => c.AssignedAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ResolvedByUser)
            .WithMany()
            .HasForeignKey(c => c.ResolvedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
