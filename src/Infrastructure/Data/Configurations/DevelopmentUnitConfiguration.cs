using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class DevelopmentUnitConfiguration : IEntityTypeConfiguration<DevelopmentUnit>
{
    public void Configure(EntityTypeBuilder<DevelopmentUnit> builder)
    {
        builder.Property(u => u.UnitIdentifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.UnitType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Status)
            .HasConversion<string>();

        builder.Property(u => u.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(u => u.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(u => u.Description)
            .HasMaxLength(2000);

        builder.Property(u => u.Period)
            .HasMaxLength(50);

        builder.Property(u => u.ListingType)
            .HasConversion<string>();

        builder.Property(u => u.PropertyType)
            .HasConversion<string>();

        builder.Property(u => u.Amenities)
            .HasColumnType("text[]");

        builder.HasIndex(u => new { u.DevelopmentProjectId, u.UnitIdentifier })
            .IsUnique();

        // One listing per unit. The unique index is what actually enforces it, so
        // promoting two units at once cannot produce duplicate listings.
        builder.HasIndex(u => u.PropertyId)
            .IsUnique();

        builder.HasOne(u => u.Project)
            .WithMany(p => p.Units)
            .HasForeignKey(u => u.DevelopmentProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // A unit keeps its history if its listing is deleted, so the unit falls
        // back to "not yet listed" rather than disappearing with the property.
        builder.HasOne(u => u.Property)
            .WithMany()
            .HasForeignKey(u => u.PropertyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.TrackedBy)
            .WithOne(t => t.Unit)
            .HasForeignKey(t => t.DevelopmentUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
