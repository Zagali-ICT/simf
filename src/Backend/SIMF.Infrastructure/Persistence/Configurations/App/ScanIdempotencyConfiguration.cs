using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-148 — 24-hour idempotency replay store for POST /scans.
/// Composite PK on (Key, GateId) — same key can be replayed against the same
/// gate; cross-gate reuse is a conflict at the service layer.</summary>
internal sealed class ScanIdempotencyConfiguration
    : IEntityTypeConfiguration<ScanIdempotency>
{
    public void Configure(EntityTypeBuilder<ScanIdempotency> builder)
    {
        builder.ToTable("ScanIdempotency");
        builder.HasKey(idem => new { idem.Key, idem.GateId });

        builder.Property(idem => idem.Key).HasMaxLength(64).IsRequired();
        builder.Property(idem => idem.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(idem => idem.ResponseHash).HasMaxLength(128).IsRequired();

        builder.HasIndex(idem => idem.StoredAt)
            .HasDatabaseName("IX_ScanIdempotency_StoredAt");
    }
}
