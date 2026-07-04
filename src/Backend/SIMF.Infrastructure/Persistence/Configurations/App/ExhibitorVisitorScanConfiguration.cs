using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-426 — ExhibitorVisitorScan EF config. Indexed by exhibitor (the
/// "My Visitors" list query) and by (exhibitor, visitor) for the
/// idempotent-capture lookup. Both refs are bare-Guid logical FKs to
/// <c>SimfUser.Id</c> on the Identity DB — no DB FK (D-157). Auto-discovered by
/// <c>ApplyConfigurationsFromAssembly</c> (App namespace).</summary>
internal sealed class ExhibitorVisitorScanConfiguration
    : IEntityTypeConfiguration<ExhibitorVisitorScan>
{
    public void Configure(EntityTypeBuilder<ExhibitorVisitorScan> builder)
    {
        builder.ToTable("ExhibitorVisitorScans");
        builder.HasKey(scan => scan.Id);
        builder.Property(scan => scan.Note).HasMaxLength(512);
        // D-611 (Wave B) — one ACTIVE capture per (exhibitor, visitor): the
        // filtered unique replaces the plain composite and backs the idempotent
        // capture. The standalone HasIndex(ExhibitorUserId) is dropped — a
        // redundant left-prefix of this composite.
        builder.HasIndex(scan => new { scan.ExhibitorUserId, scan.VisitorUserId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
