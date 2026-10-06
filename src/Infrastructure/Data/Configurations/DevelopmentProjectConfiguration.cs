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

        // The listing a project represents. One-to-one, because a property may
        // represent at most one development project; the unique index is what
        // stops two projects claiming the same listing. Matches the
        // IX_DevelopmentProjects_PropertyId unique index and the SET NULL foreign
        // key already present in the database.
        builder.HasOne(p => p.Property)
            .WithOne()
            .HasForeignKey<DevelopmentProject>(p => p.PropertyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.PropertyId)
            .IsUnique();

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
