using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddSeatReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HallSeatLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowLabels = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SeatsPerRow = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallSeatLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallSeatLayouts_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeatReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowLabel = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    SeatNumber = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ReservedForUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeatReservations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallSeatLayouts_HallId",
                table: "HallSeatLayouts",
                column: "HallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_ReservedForUserId_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "ReservedForUserId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_ReleasedAt",
                table: "SeatReservations",
                columns: new[] { "SessionId", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_ReservedForUserId",
                table: "SeatReservations",
                columns: new[] { "SessionId", "ReservedForUserId" },
                unique: true,
                filter: "[ReleasedAt] IS NULL AND [ReservedForUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_SessionId_RowLabel_SeatNumber",
                table: "SeatReservations",
                columns: new[] { "SessionId", "RowLabel", "SeatNumber" },
                unique: true,
                filter: "[ReleasedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallSeatLayouts");

            migrationBuilder.DropTable(
                name: "SeatReservations");
        }
    }
}
