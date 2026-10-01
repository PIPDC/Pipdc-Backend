using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(100);

        // Name is unique per parent, not globally: an LGA named "Ikeja" and a city
        // named "Ikeja" must both be allowed, and so must a city name that repeats
        // under different LGAs. Two indexes express that:
        //   1. UNIQUE (Name, ParentId)  -> children of the same parent
        //   2. UNIQUE (Name) WHERE "ParentId" IS NULL -> root rows
        // The second one is required because in PostgreSQL NULL values are
        // distinct, so index 1 alone does NOT prevent two states sharing a name.
        // (A plain global UNIQUE on Name would be wrong and is not used.)
        builder.HasIndex(l => l.Name)
            .IsUnique()
            .HasFilter("\"ParentId\" IS NULL")
            .HasDatabaseName("IX_Locations_Name_Root");

        builder.Property(l => l.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(l => l.Slug)
            .IsUnique();

        builder.Property(l => l.Type)
            .HasConversion<string>();

        builder.HasIndex(l => l.Type);

        builder.HasOne(l => l.Parent)
            .WithMany(l => l.Children)
            .HasForeignKey(l => l.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Explicit FK index. The index already exists in the database (created with
        // the location FK) but was not declared in the model, so EF would otherwise
        // try to drop it on the next migration.
        builder.HasIndex(l => l.ParentId);

        builder.HasIndex(l => new { l.Name, l.ParentId })
            .IsUnique()
            .HasDatabaseName("IX_Locations_Name_ParentId");
    }
}
