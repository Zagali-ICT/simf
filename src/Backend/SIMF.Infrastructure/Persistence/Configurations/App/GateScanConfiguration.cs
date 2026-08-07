using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// Append-only audit log of every scan. Five non-clustered indexes ride the
/// clustered bigint IDENTITY PK. Opts out of <c>RowAudit</c> because it is
/// itself an audit log: it already carries the scanning user, the correlation id,
/// the IP and the user agent, so auditing it would double the write volume for
/// zero gain. The opt-out lives in the excluded-entity set of
/// <c>RowAuditingSaveChangesInterceptor</c>, where each entry states its reason.
/// </summary>
internal sealed class GateScanConfiguration : IEntityTypeConfiguration<GateScan>
{
    public void Configure(EntityTypeBuilder<GateScan> builder)
    {
        // Pin the Outcome↔DenialReason invariant at the DB:
        // a Denied scan (Outcome=1) always carries a reason code, an Allowed scan
        // (Outcome=0) never does. Outcome/DenialReasonCode are stored as int.
        builder.ToTable("GateScans", table => table.HasCheckConstraint(
            "CK_GateScans_DenialPin",
            "([Outcome] = 1 AND [DenialReasonCode] IS NOT NULL) OR " +
            "([Outcome] = 0 AND [DenialReasonCode] IS NULL)"));
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Id).ValueGeneratedOnAdd();

        // Widened 32 -> 64. The offline event badge is an ENCRYPTED
        // payload (~54 chars), not a bare 12-character serial, and the scanner
        // sends the whole blob so the SERVER decrypts it independently rather
        // than trusting the device's result. That keeps this audit column
        // exactly what was physically presented at the gate, which is the point
        // of an append-only scan log.
        builder.Property(scan => scan.QrIdAtScan).HasMaxLength(96).IsRequired();
        // Snapshot fields capture the visitor's identity at the
        // moment of the scan so the audit row survives cross-DB drift.
        builder.Property(scan => scan.ScannedDisplayName).HasMaxLength(128);
        builder.Property(scan => scan.ScannedProfileTypeName).HasMaxLength(128);
        builder.Property(scan => scan.Direction).HasConversion<int>();
        builder.Property(scan => scan.Outcome).HasConversion<int>();
        builder.Property(scan => scan.DenialReasonCode).HasConversion<int?>();
        builder.Property(scan => scan.Source).HasConversion<int>();
        builder.Property(scan => scan.CorrelationId).HasMaxLength(64);
        builder.Property(scan => scan.IpAddress).HasMaxLength(64);
        builder.Property(scan => scan.UserAgent).HasMaxLength(512);
        builder.Property(scan => scan.IdempotencyKey).HasMaxLength(64);

        builder.HasIndex(scan => new { scan.GateId, scan.ScannedAt })
            .HasDatabaseName("IX_GateScan_Gate_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAt })
            .HasDatabaseName("IX_GateScan_UserProfile_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAt })
            .HasDatabaseName("IX_GateScan_UserProfile_LastAllowed")
            .IsDescending(false, true)
            .HasFilter("[Outcome] = 0 AND [UserProfileId] IS NOT NULL");

        builder.HasIndex(scan => new { scan.GateId, scan.UserProfileId, scan.ScannedAt })
            .HasDatabaseName("IX_GateScan_Gate_UserProfile_5sWindow")
            .IsDescending(false, false, true)
            .HasFilter("[UserProfileId] IS NOT NULL");

        builder.HasIndex(scan => new { scan.ScannedByUserId, scan.ScannedAt })
            .HasDatabaseName("IX_GateScan_ScannedBy_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.IdempotencyKey, scan.GateId })
            .HasDatabaseName("UX_GateScan_Idempotency")
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        // Gate is on the App DB; make the relationship explicit
        // Restrict (was Cascade by convention) so deleting a Gate can't wipe its
        // append-only scan history. The IX_GateScan_Gate_ScannedAt index (GateId
        // leading) already covers the FK, so no duplicate index is created.
        builder.HasOne(scan => scan.Gate)
            .WithMany()
            .HasForeignKey(scan => scan.GateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
