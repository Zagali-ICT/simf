using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// Append-only audit log of every scan. Six non-clustered indexes ride the
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
        // Every enum on this table is stored as int, so the database on its own
        // would accept any 32-bit number. On an append-only log a garbage value
        // can never be corrected afterwards, so each enum column is pinned to its
        // declared range. The bounds are written as literals, matching the house
        // style (CK_VenueMapNodes_KindArc, CK_RatingAnswers_Stars); appending a
        // new enum member means widening the matching bound here.
        builder.ToTable("GateScans", table =>
        {
            // A Denied scan (Outcome=1) always carries a reason code, an Allowed
            // scan (Outcome=0) never does. The two branches between them also
            // confine Outcome to {0,1}, so ScanOutcome needs no separate range
            // check.
            table.HasCheckConstraint(
                "CK_GateScans_DenialPin",
                "([Outcome] = 1 AND [DenialReasonCode] IS NOT NULL) OR " +
                "([Outcome] = 0 AND [DenialReasonCode] IS NULL)");

            // ScanDirection: CheckIn=0 .. CheckOut=1.
            table.HasCheckConstraint(
                "CK_GateScans_DirectionRange", "[Direction] BETWEEN 0 AND 1");

            // ScanSource: Simulator=0 .. Kiosk=2. Note that 0 is also what an
            // unset int lands on, so this check cannot catch a forgotten write --
            // it only keeps an out-of-range value out of the log.
            table.HasCheckConstraint(
                "CK_GateScans_SourceRange", "[Source] BETWEEN 0 AND 2");

            // DenialReasonCode: QrUnknown=0 .. BookingRequiredMissing=8, null on
            // an allowed scan (which CK_GateScans_DenialPin already requires).
            table.HasCheckConstraint(
                "CK_GateScans_DenialReasonRange",
                "[DenialReasonCode] IS NULL OR [DenialReasonCode] BETWEEN 0 AND 8");
        });
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Id).ValueGeneratedOnAdd();

        // 96, with headroom over the ~54-character encrypted badge payload. The
        // offline event badge is an ENCRYPTED blob, not a bare 12-character
        // serial, and the scanner sends the whole blob so the SERVER decrypts it
        // independently rather than trusting the device's result. That keeps this
        // audit column exactly what was physically presented at the gate, which
        // is the point of an append-only scan log. GateOperatorService pins its
        // own inbound length guard to this number, so the two move together.
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

        // Both of these cover (UserProfileId, ScannedAt) and differ only in the
        // filter, so each needs the NAMED HasIndex overload. The unnamed one is
        // keyed on the property set alone: a second unnamed call over the same
        // pair reconfigures the first rather than declaring a second index, and
        // it did - the history index below was silently renamed and filtered out
        // of existence, leaving one index where the migration should build two.
        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAt }, "IX_GateScan_UserProfile_ScannedAt")
            .HasDatabaseName("IX_GateScan_UserProfile_ScannedAt")
            .IsDescending(false, true);

        builder.HasIndex(scan => new { scan.UserProfileId, scan.ScannedAt }, "IX_GateScan_UserProfile_LastAllowed")
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
