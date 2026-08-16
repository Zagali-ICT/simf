using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common.Options;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Hall entity configuration.
/// Mirrors <see cref="ThemeConfiguration"/>; unique index on <c>Code</c>.</summary>
internal sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        // Geofence columns are all-null together, or all-set with a coordinate
        // that is actually a coordinate and a radius inside the venue-scale cap.
        // The three range clauses mirror AdminHallService.ValidateGeofence, which
        // is where the same rule is stated in prose on the entity and enforced for
        // the admin path; without them the column comment described a rule the
        // database did not hold, and any writer that is not that service (a seed,
        // an import, a repair script) could store latitude 900.
        // The arrival grace is null (inherit) or within the one shared bound.
        // Capacity is zero or positive: zero is a real value (a hall recorded
        // before its seating is known), which is why this is >= 0 and not > 0.
        builder.ToTable("Halls", table =>
        {
            table.HasCheckConstraint(
                "CK_Halls_Geofence",
                "([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceCenterLat] >= -90 AND [GeofenceCenterLat] <= 90 AND [GeofenceCenterLon] >= -180 AND [GeofenceCenterLon] <= 180 AND [GeofenceRadiusMeters] > 0 AND [GeofenceRadiusMeters] <= 100000)");
            table.HasCheckConstraint(
                "CK_Halls_ArrivalGrace",
                $"[ArrivalGraceMinutes] IS NULL OR ([ArrivalGraceMinutes] >= 0 AND [ArrivalGraceMinutes] <= {WalkInModeOptions.MaxArrivalGraceMinutes})");
            table.HasCheckConstraint("CK_Halls_Capacity", "[Capacity] >= 0");
        });
        builder.HasKey(hall => hall.Id);

        builder.Property(hall => hall.Code).HasMaxLength(16).IsRequired();
        builder.Property(hall => hall.Name).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.Floor).HasMaxLength(32);
        builder.Property(hall => hall.FacilityNotes).HasMaxLength(1024);

        builder.HasIndex(hall => hall.Code).IsUnique();
        builder.HasIndex(hall => new { hall.IsActive, hall.Name });
    }
}
