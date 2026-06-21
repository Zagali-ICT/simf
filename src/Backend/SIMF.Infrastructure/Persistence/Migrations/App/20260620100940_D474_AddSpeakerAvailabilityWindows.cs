using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D474_AddSpeakerAvailabilityWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlotEndUtc",
                table: "SpeakerMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SlotStartUtc",
                table: "SpeakerMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpeakerAvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpeakerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpeakerAvailabilityWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpeakerAvailabilityWindows_Speakers_SpeakerId",
                        column: x => x.SpeakerId,
                        principalTable: "Speakers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerAvailabilityWindows_SpeakerId_IsActive_StartUtc",
                table: "SpeakerAvailabilityWindows",
                columns: new[] { "SpeakerId", "IsActive", "StartUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpeakerAvailabilityWindows");

            migrationBuilder.DropColumn(
                name: "SlotEndUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "SlotStartUtc",
                table: "SpeakerMeetingRequests");
        }
    }
}
