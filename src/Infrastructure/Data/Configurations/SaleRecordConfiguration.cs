using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class SaleRecordConfiguration : IEntityTypeConfiguration<SaleRecord>
{
    public void Configure(EntityTypeBuilder<SaleRecord> builder)
    {
        builder.Property(s => s.SalePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(s => s.BuyerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.BuyerContact)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Notes)
            .HasMaxLength(4000);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(TransactionStatus.Pending);

        builder.Property(s => s.BuyerUserId).HasMaxLength(450);
        builder.Property(s => s.RecordedByUserId).IsRequired().HasMaxLength(450);

        // Batch 7: the enquiry-to-deal join that makes conversion measurable, and
        // the lookups for the admin transaction list.
        builder.HasIndex(s => s.EnquiryId);
        builder.HasIndex(s => s.RecordedByUserId);

        builder.HasOne(s => s.Property)
            .WithOne(p => p.SaleRecord)
            .HasForeignKey<SaleRecord>(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull, not Cascade: deleting an enquiry must never silently destroy a
        // recorded sale. The sale is the legally meaningful row; the enquiry link
        // is only provenance.
        builder.HasOne(s => s.Enquiry)
            .WithMany()
            .HasForeignKey(s => s.EnquiryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(s => s.Buyer)
            .WithMany()
            .HasForeignKey(s => s.BuyerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict: a sale must always name the person who recorded it, so that
        // account cannot be deleted while the record points at it.
        builder.HasOne(s => s.RecordedByUser)
            .WithMany()
            .HasForeignKey(s => s.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
