using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D743_AddSeatReservationExpiresUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresUtc",
                table: "SeatReservations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ExpiresUtc",
                table: "SeatReservations",
                column: "ExpiresUtc",
                filter: "[ReleasedAt] IS NULL AND [ExpiresUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_ExpiresUtc",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ExpiresUtc",
                table: "SeatReservations");
        }
    }
}
