using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D716_AddSpeakerMeetingHallBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "HallId",
                table: "SpeakerMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MeetingTableId",
                table: "SpeakerMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SpeakerDecisionAt",
                table: "SpeakerMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "HallId", "SlotStartUtc" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_MeetingTableId",
                table: "SpeakerMeetingRequests",
                column: "MeetingTableId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStartUtc" },
                unique: true,
                filter: "[SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerMeetingRequests_Halls_HallId",
                table: "SpeakerMeetingRequests",
                column: "HallId",
                principalTable: "Halls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerMeetingRequests_MeetingTables_MeetingTableId",
                table: "SpeakerMeetingRequests",
                column: "MeetingTableId",
                principalTable: "MeetingTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SpeakerMeetingRequests_Halls_HallId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_SpeakerMeetingRequests_MeetingTables_MeetingTableId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_HallId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_MeetingTableId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "HallId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "MeetingTableId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "SpeakerDecisionAt",
                table: "SpeakerMeetingRequests");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_SpeakerId_SlotStartUtc",
                table: "SpeakerMeetingRequests",
                columns: new[] { "SpeakerId", "SlotStartUtc" },
                unique: true,
                filter: "[Status] = 1 AND [SlotStartUtc] IS NOT NULL");
        }
    }
}
