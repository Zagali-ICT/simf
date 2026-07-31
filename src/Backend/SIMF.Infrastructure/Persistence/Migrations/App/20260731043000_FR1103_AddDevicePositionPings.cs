using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>FR-1103 (owner decision Q6) — one ADDITIVE table,
    /// <c>DevicePositionPings</c>, giving movement / dwell / route tracking the
    /// capture path it never had. Nothing existing is renamed, dropped or altered,
    /// and no shipped JSON field changes.
    ///
    /// <para>Deliberately FK-free (see <c>DevicePositionPingConfiguration</c>):
    /// these are raw telemetry rows written at device cadence, and one must never
    /// be the reason a hall cannot be edited or a session removed. The two indexes
    /// serve the only two reads — the per-attendee route projection
    /// (<c>UserId, CapturedAt</c>) and the per-hall dwell aggregation
    /// (<c>HallId, CapturedAt</c>).</para>
    ///
    /// <para>The table ships EMPTY and stays that way until a hall is given a
    /// geofence boundary from the CP: with no boundary configured, every ping
    /// resolves to a null <c>HallId</c> and the dwell aggregation reports
    /// nothing.</para></summary>
    public partial class FR1103_AddDevicePositionPings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DevicePositionPings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevicePositionPings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePositionPings_HallId_CapturedAt",
                table: "DevicePositionPings",
                columns: new[] { "HallId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DevicePositionPings_UserId_CapturedAt",
                table: "DevicePositionPings",
                columns: new[] { "UserId", "CapturedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DevicePositionPings");
        }
    }
}
