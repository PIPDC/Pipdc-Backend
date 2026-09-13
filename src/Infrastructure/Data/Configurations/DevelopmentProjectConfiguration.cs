using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class DevelopmentProjectConfiguration : IEntityTypeConfiguration<DevelopmentProject>
{
    public void Configure(EntityTypeBuilder<DevelopmentProject> builder)
    {
        builder.Property(p => p.Name)
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

        builder.Property(p => p.Location)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Developer)
            .HasMaxLength(200);

        builder.Property(p => p.Status)
            .HasConversion<string>();

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.Featured);

        builder.HasMany(p => p.Units)
            .WithOne(u => u.Project)
            .HasForeignKey(u => u.DevelopmentProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Updates)
            .WithOne(u => u.Project)
            .HasForeignKey(u => u.DevelopmentProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Project)
            .HasForeignKey(i => i.DevelopmentProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.TrackedBy)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.DevelopmentProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.LocationRef)
            .WithMany(l => l.DevelopmentProjects)
            .HasForeignKey(p => p.LocationRefId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
