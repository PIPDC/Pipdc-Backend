using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentApplicationBlockConfiguration : IEntityTypeConfiguration<AgentApplicationBlock>
{
    public void Configure(EntityTypeBuilder<AgentApplicationBlock> builder)
    {
        builder.Property(b => b.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(b => b.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.LiftedByAdminId)
            .HasMaxLength(450);

        // The administrator who set the bar. Required, not nullable: a permanent
        // bar with no attributable decision-maker cannot be defended when the
        // applicant asks why.
        builder.Property(b => b.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        // One bar per account, ever. The row is retained after it is lifted so the
        // history of who was barred and why is not lost, which means the index is
        // on UserId alone and is not a partial index.
        builder.HasIndex(b => b.UserId)
            .IsUnique()
            .HasDatabaseName("IX_AgentApplicationBlocks_UserId");

        builder.HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
