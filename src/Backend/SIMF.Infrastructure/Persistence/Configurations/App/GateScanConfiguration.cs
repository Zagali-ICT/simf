using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// D-148 — append-only audit log of every scan (SIMF-FDS-003 §5.6 / API-GATES-001).
/// Five non-clustered indexes per SIMF-DAT-001 §5.3.2 ride the clustered bigint
/// IDENTITY PK. Opts out of <c>RowAudit</c> because it is itself an audit log
/// (D-148 rationale — the per-decision opt-out D-135 (e) requires).
/// </summary>
internal sealed class GateScanConfiguration : IEntityTypeConfiguration<GateScan>
{
    public void Configure(EntityTypeBuilder<GateScan> builder)
    {
        builder.ToTable("GateScans");
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Id).ValueGeneratedOnAdd();

        builder.Property(scan => scan.QrIdAtScan).HasMaxLength(32).IsRequired();
        builder.Property(scan => scan.Direction).HasConversion<int>();
        builder.Property(scan => scan.Outcome).HasConversion<int>();
        builder.Property(scan => scan.DenialReasonCode).HasConversion<int?>();
        builder.Property(scan => scan.Source).HasConversion<int>();
        builder.Property(scan => scan.CorrelationId).HasMaxLength(64);
        builder.Property(scan => scan.IpAddress).HasMaxLength(64);
        builder.Property(scan => scan.UserAgent).HasMaxLength(512);
        builder.Property(scan => scan.IdempotencyKey).HasMaxLength(64);

        builder.HasIndex(scan => new { scan.GateId, scan.ScannedAtUtc })
            .HasDatabaseName("IX_GateScan_Gate_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAtUtc })
            .HasDatabaseName("IX_GateScan_UserProfile_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAtUtc })
            .HasDatabaseName("IX_GateScan_UserProfile_LastAllowed")
            .IsDescending(false, true)
            .HasFilter("[Outcome] = 0 AND [UserProfileId] IS NOT NULL");

        builder.HasIndex(scan => new { scan.GateId, scan.UserProfileId, scan.ScannedAtUtc })
            .HasDatabaseName("IX_GateScan_Gate_UserProfile_5sWindow")
            .IsDescending(false, false, true)
            .HasFilter("[UserProfileId] IS NOT NULL");

        builder.HasIndex(scan => new { scan.ScannedByUserId, scan.ScannedAtUtc })
            .HasDatabaseName("IX_GateScan_ScannedBy_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.IdempotencyKey, scan.GateId })
            .HasDatabaseName("UX_GateScan_Idempotency")
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
