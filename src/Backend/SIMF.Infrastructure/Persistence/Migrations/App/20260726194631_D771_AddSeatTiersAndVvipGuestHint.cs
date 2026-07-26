using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D771_AddSeatTiersAndVvipGuestHint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestHint",
                table: "SeatReservations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestHintArabic",
                table: "SeatReservations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeatTiers",
                table: "HallSeatLayouts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestHint",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "GuestHintArabic",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "SeatTiers",
                table: "HallSeatLayouts");
        }
    }
}
