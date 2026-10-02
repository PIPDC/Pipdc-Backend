using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class LeaseRecordConfiguration : IEntityTypeConfiguration<LeaseRecord>
{
    public void Configure(EntityTypeBuilder<LeaseRecord> builder)
    {
        builder.Property(l => l.TenantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.TenantContact)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(l => l.MonthlyRent)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.Notes)
            .HasMaxLength(4000);

        builder.Property(l => l.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(TransactionStatus.Pending);

        builder.Property(l => l.TenantUserId).HasMaxLength(450);
        builder.Property(l => l.RecordedByUserId).IsRequired().HasMaxLength(450);

        // Batch 7: enquiry provenance plus the admin transaction list lookups.
        builder.HasIndex(l => l.EnquiryId);
        builder.HasIndex(l => l.RecordedByUserId);

        // The real duplicate guard, enforced by the database rather than by a
        // read-then-write check that two concurrent requests could both pass.
        // At most one live tenancy per property; terminated, completed and
        // cancelled rows are history and do not block a re-let.
        builder.HasIndex(l => l.PropertyId)
            .IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Active')")
            .HasDatabaseName("IX_LeaseRecords_OneLiveLeasePerProperty");

        // Supports the "what is live now" checks and the admin list filter.
        builder.HasIndex(l => new { l.Status, l.LeaseEndDate });

        builder.HasOne(l => l.Property)
            .WithMany(p => p.LeaseRecords)
            .HasForeignKey(l => l.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull, matching SaleRecord: provenance is not the valuable row.
        builder.HasOne(l => l.Enquiry)
            .WithMany()
            .HasForeignKey(l => l.EnquiryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Tenant)
            .WithMany()
            .HasForeignKey(l => l.TenantUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.RecordedByUser)
            .WithMany()
            .HasForeignKey(l => l.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
