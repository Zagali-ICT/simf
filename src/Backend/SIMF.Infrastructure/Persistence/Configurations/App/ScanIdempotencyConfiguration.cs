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

        // Both hashes are OpaqueToken.Hash output: SHA-256 as lowercase hex, so
        // exactly 64 characters drawn from [0-9a-f]. nvarchar(128) was double the
        // width and double the bytes per character for an alphabet that is pure
        // ASCII. varchar(64) is the honest type.
        //
        // Deliberately NOT IsFixedLength(), which is what the MeetingActionToken
        // hash columns use: char(N) pads short values with trailing spaces on
        // read, and TryReplayAsync compares RequestHash in C# with
        // StringComparison.Ordinal, where a padded value would silently stop
        // matching. Those columns get away with char because they are compared in
        // SQL, which ignores trailing spaces. This one is not.
        builder.Property(idem => idem.RequestHash)
            .HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(idem => idem.ResponseHash)
            .HasMaxLength(64).IsUnicode(false).IsRequired();

        builder.HasIndex(idem => idem.StoredAt)
            .HasDatabaseName("IX_ScanIdempotency_StoredAt");
    }
}
