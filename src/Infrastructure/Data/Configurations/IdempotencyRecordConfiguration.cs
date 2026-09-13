using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PIPDC.Domain.Entities;

namespace PIPDC.Infrastructure.Data.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.Property(r => r.Key)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.RequestHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.UserId)
            .HasMaxLength(450);

        // Authenticated scope: one key per user. Postgres ignores rows where a
        // composite-index column is NULL, so anonymous rows never conflict here.
        builder.HasIndex(r => new { r.UserId, r.Key })
            .IsUnique();

        // Anonymous scope (auth POSTs): one key globally among anonymous requests.
        builder.HasIndex(r => r.Key)
            .IsUnique()
            .HasFilter("\"UserId\" IS NULL");

        // Supports the opportunistic purge of expired records on write.
        builder.HasIndex(r => r.ExpiresAt);
    }
}