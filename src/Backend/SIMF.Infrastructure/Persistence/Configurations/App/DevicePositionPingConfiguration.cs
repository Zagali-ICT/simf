using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>FR-1103 (Q6) — <see cref="DevicePositionPing"/> configuration.
/// Deliberately FK-free: these are raw telemetry rows written at device cadence,
/// and one must never be the reason a hall cannot be edited or a session removed
/// (unlike <c>HallAttendance</c>, which IS a business record and keeps its
/// Restrict FKs). The two indexes serve the only two reads: the per-attendee route
/// projection and the per-hall dwell aggregation.</summary>
internal sealed class DevicePositionPingConfiguration : IEntityTypeConfiguration<DevicePositionPing>
{
    public void Configure(EntityTypeBuilder<DevicePositionPing> builder)
    {
        builder.ToTable("DevicePositionPings");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CapturedAt).IsRequired();
        builder.Property(p => p.Latitude).IsRequired();
        builder.Property(p => p.Longitude).IsRequired();

        // The route projection: one attendee's track, in capture order.
        builder.HasIndex(p => new { p.UserId, p.CapturedAt });

        // The dwell aggregation: everything captured inside one hall's boundary
        // over a window.
        builder.HasIndex(p => new { p.HallId, p.CapturedAt });
    }
}
