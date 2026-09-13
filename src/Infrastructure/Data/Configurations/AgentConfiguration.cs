using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.Property(a => a.Bio)
            .HasMaxLength(4000);

        builder.Property(a => a.Title)
            .HasMaxLength(100);

        builder.Property(a => a.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(a => a.PhotoPublicId)
            .HasMaxLength(200);

        builder.Property(a => a.AgencyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.LicenseNumber)
            .HasMaxLength(100);

        builder.Property(a => a.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(a => a.User)
            .WithOne(u => u.Agent)
            .HasForeignKey<Agent>(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("xmin").IsRowVersion();
    }
}
