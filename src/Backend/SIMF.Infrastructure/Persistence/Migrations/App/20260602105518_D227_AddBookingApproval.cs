using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D227_AddBookingApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "SeatReservations",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "SeatReservations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "SeatReservations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SeatReservations",
                type: "int",
                nullable: false,
                // P2.2 — D-227: one-time backfill of existing (pre-workflow) rows
                // to Approved (1) — they were confirmed before the approval queue
                // existed. New inserts always send Status explicitly (the model
                // carries no default), so this constraint only affects the backfill.
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_Status_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "Status", "ReleasedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_Status_ReleasedAt",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SeatReservations");
        }
    }
}
