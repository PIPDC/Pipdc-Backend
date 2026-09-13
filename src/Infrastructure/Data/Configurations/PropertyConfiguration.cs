using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;
using PIPDC.Domain.Enums;

namespace PIPDC.Infrastructure.Data.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(p => p.Period)
            .HasMaxLength(50);

        builder.Property(p => p.SizeUnit)
            .HasMaxLength(10);

        builder.Property(p => p.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.State)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Area)
            .HasMaxLength(100);

        builder.Property(p => p.PropertyType)
            .HasConversion<string>();

        builder.Property(p => p.ListingType)
            .HasConversion<string>();

        builder.Property(p => p.Status)
            .HasConversion<string>();

        builder.HasIndex(p => new { p.ListingType, p.Status });
        builder.HasIndex(p => p.City);
        builder.HasIndex(p => p.State);
        builder.HasIndex(p => p.Featured);

        builder.HasOne(p => p.Agent)
            .WithMany(a => a.Properties)
            .HasForeignKey(p => p.AgentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany()
            .HasForeignKey(p => p.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.CreatedByUserId);

        builder.HasMany(p => p.PropertyImages)
            .WithOne(i => i.Property)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Enquiries)
            .WithOne(e => e.Property)
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.SavedByUsers)
            .WithOne(s => s.Property)
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.SaleRecord)
            .WithOne(s => s.Property)
            .HasForeignKey<SaleRecord>(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.LeaseRecord)
            .WithOne(l => l.Property)
            .HasForeignKey<LeaseRecord>(l => l.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Location)
            .WithMany(l => l.Properties)
            .HasForeignKey(p => p.LocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
