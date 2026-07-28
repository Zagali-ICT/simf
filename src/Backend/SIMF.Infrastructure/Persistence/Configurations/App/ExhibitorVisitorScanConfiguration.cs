using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-426 — ExhibitorVisitorScan EF config. Indexed by (booth, visitor)
/// for the "My Visitors" list query and the idempotent capture, and by
/// (officer, visitor) for the legacy un-backfilled rows. The two USER refs are
/// bare-Guid logical FKs to <c>SimfUser.Id</c> on the Identity DB — no DB FK
/// (D-157); <c>ExhibitorId</c> is a real FK because both tables live on the App
/// DB. Auto-discovered by <c>ApplyConfigurationsFromAssembly</c> (App
/// namespace).</summary>
internal sealed class ExhibitorVisitorScanConfiguration
    : IEntityTypeConfiguration<ExhibitorVisitorScan>
{
    public void Configure(EntityTypeBuilder<ExhibitorVisitorScan> builder)
    {
        builder.ToTable("ExhibitorVisitorScans");
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Note).HasMaxLength(512);
        // D-611 (Wave B) made this unique — one ACTIVE capture per
        // (officer, visitor). FR-EXH-003 moved the ownership to the BOOTH, so
        // that is no longer the invariant and the uniqueness moved with it (see
        // the booth index below). It stays as a plain index because it still
        // serves the legacy fallback lookup — an un-backfilled row is found by
        // (ExhibitorId IS NULL, ExhibitorUserId).
        builder.HasIndex(scan => new { scan.ExhibitorUserId, scan.VisitorUserId });

        // FR-EXH-003 — the booth the capture belongs to. Exhibitor and
        // ExhibitorVisitorScan are both on the App DB, so this is a real FK;
        // Restrict (never Cascade) so closing a booth cannot silently delete its
        // lead history — the same rule ExhibitorMembership follows.
        builder.HasOne(scan => scan.Exhibitor)
            .WithMany()
            .HasForeignKey(scan => scan.ExhibitorId)
            .OnDelete(DeleteBehavior.Restrict);

        // One ACTIVE capture per (BOOTH, visitor) — the invariant under the new
        // ownership model, backing the booth-scoped list query and the
        // idempotent capture. Null ExhibitorId is excluded from the filter: those
        // are the legacy un-backfilled rows, which are never created any more and
        // whose uniqueness the plain index above no longer claims to enforce.
        builder.HasIndex(scan => new { scan.ExhibitorId, scan.VisitorUserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [ExhibitorId] IS NOT NULL");
    }
}
