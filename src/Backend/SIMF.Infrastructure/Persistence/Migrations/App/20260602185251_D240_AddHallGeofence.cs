using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D240_AddHallGeofence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GeofenceCenterLat",
                table: "Halls",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GeofenceCenterLon",
                table: "Halls",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GeofenceRadiusMeters",
                table: "Halls",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeofenceCenterLat",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "GeofenceCenterLon",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "GeofenceRadiusMeters",
                table: "Halls");
        }
    }
}
