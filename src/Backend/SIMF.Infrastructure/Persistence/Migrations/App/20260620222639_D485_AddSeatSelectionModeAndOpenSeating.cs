using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D485_AddSeatSelectionModeAndOpenSeating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations");

            migrationBuilder.AddColumn<int>(
                name: "SeatSelectionModeOverride",
                table: "Sessions",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SeatNumber",
                table: "SeatReservations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "RowLabel",
                table: "SeatReservations",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8);

            migrationBuilder.AddColumn<int>(
                name: "SeatSelectionMode",
                table: "Halls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations",
                columns: new[] { "SessionId", "RowLabel", "SeatNumber" },
                unique: true,
                filter: "[ReleasedAt] IS NULL AND [RowLabel] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "SeatSelectionModeOverride",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SeatSelectionMode",
                table: "Halls");

            migrationBuilder.AlterColumn<int>(
                name: "SeatNumber",
                table: "SeatReservations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RowLabel",
                table: "SeatReservations",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(8)",
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations",
                columns: new[] { "SessionId", "RowLabel", "SeatNumber" },
                unique: true,
                filter: "[ReleasedAt] IS NULL");
        }
    }
}
