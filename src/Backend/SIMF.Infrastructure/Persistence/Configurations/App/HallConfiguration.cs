using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-134 Sprint B — Hall entity configuration (D-135 freeze-lift).
/// Mirrors <see cref="ThemeConfiguration"/>; unique index on <c>Code</c>.</summary>
internal sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        // D-611 (Wave B) — geofence columns are all-null together, or all-set with a positive radius.
        builder.ToTable("Halls", table => table.HasCheckConstraint(
            "CK_Halls_Geofence",
            "([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceRadiusMeters] > 0)"));
        builder.HasKey(hall => hall.Id);

        builder.Property(hall => hall.Code).HasMaxLength(16).IsRequired();
        builder.Property(hall => hall.Name).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.Floor).HasMaxLength(32);
        builder.Property(hall => hall.EquipmentNotes).HasMaxLength(1024);

        builder.HasIndex(hall => hall.Code).IsUnique();
        builder.HasIndex(hall => new { hall.IsActive, hall.Name });
    }
}
