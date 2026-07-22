using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddMeetingHallBindConfirmAndCheckin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckedInAt",
                table: "SpeakerMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckedInByUserId",
                table: "SpeakerMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentUtc",
                table: "SpeakerMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AvailabilityWindowId",
                table: "DelegationMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckedInAt",
                table: "DelegationMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckedInByUserId",
                table: "DelegationMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "DelegationMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmedByUserId",
                table: "DelegationMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HallId",
                table: "DelegationMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MeetingTableId",
                table: "DelegationMeetingRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentUtc",
                table: "DelegationMeetingRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_AvailabilityWindowId",
                table: "DelegationMeetingRequests",
                column: "AvailabilityWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStartUtc",
                table: "DelegationMeetingRequests",
                columns: new[] { "HallId", "SlotStartUtc" },
                unique: true,
                filter: "[HallId] IS NOT NULL AND [SlotStartUtc] IS NOT NULL AND [Status] <> 0 AND [Status] <> 2 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_MeetingTableId",
                table: "DelegationMeetingRequests",
                column: "MeetingTableId");

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId",
                table: "DelegationMeetingRequests",
                column: "AvailabilityWindowId",
                principalTable: "DelegationAvailabilityWindows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationMeetingRequests_Halls_HallId",
                table: "DelegationMeetingRequests",
                column: "HallId",
                principalTable: "Halls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DelegationMeetingRequests_MeetingTables_MeetingTableId",
                table: "DelegationMeetingRequests",
                column: "MeetingTableId",
                principalTable: "MeetingTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DelegationMeetingRequests_Halls_HallId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_DelegationMeetingRequests_MeetingTables_MeetingTableId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_HallId_SlotStartUtc",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_MeetingTableId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "CheckedInByUserId",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "ReminderSentUtc",
                table: "SpeakerMeetingRequests");

            migrationBuilder.DropColumn(
                name: "AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "CheckedInByUserId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "HallId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "MeetingTableId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "ReminderSentUtc",
                table: "DelegationMeetingRequests");
        }
    }
}
