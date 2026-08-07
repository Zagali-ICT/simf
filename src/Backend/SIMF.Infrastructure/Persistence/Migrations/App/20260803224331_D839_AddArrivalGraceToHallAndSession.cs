using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D839_AddArrivalGraceToHallAndSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArrivalGraceMinutesOverride",
                table: "Sessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArrivalGraceMinutes",
                table: "Halls",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_ArrivalGrace",
                table: "Sessions",
                sql: "[ArrivalGraceMinutesOverride] IS NULL OR ([ArrivalGraceMinutesOverride] >= 0 AND [ArrivalGraceMinutesOverride] <= 240)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Halls_ArrivalGrace",
                table: "Halls",
                sql: "[ArrivalGraceMinutes] IS NULL OR ([ArrivalGraceMinutes] >= 0 AND [ArrivalGraceMinutes] <= 240)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_ArrivalGrace",
                table: "Sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Halls_ArrivalGrace",
                table: "Halls");

            migrationBuilder.DropColumn(
                name: "ArrivalGraceMinutesOverride",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ArrivalGraceMinutes",
                table: "Halls");
        }
    }
}
