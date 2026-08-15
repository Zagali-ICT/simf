using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>24-hour idempotency replay store for POST /scans.
/// Composite PK on (Key, GateId), which scopes a key to one gate: every lookup
/// in the operator service filters on both columns, so the same key presented at
/// a second gate finds no prior record and is treated as a new scan rather than
/// a replay or a conflict. A conflict is a repeat of the same key at the same
/// gate with a different request hash.</summary>
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
