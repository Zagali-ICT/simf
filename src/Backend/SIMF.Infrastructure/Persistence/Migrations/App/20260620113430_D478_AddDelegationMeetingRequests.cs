using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D478_AddDelegationMeetingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DelegationMeetingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestingCountryId = table.Column<int>(type: "int", nullable: false),
                    TargetCountryId = table.Column<int>(type: "int", nullable: false),
                    AttendeeCount = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SlotStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SlotEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationMeetingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_Countries_RequestingCountryId",
                        column: x => x.RequestingCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingRequests_Countries_TargetCountryId",
                        column: x => x.TargetCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_RequestedByUserId",
                table: "DelegationMeetingRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_RequestingCountryId",
                table: "DelegationMeetingRequests",
                column: "RequestingCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_TargetCountryId_Status_CreatedAt",
                table: "DelegationMeetingRequests",
                columns: new[] { "TargetCountryId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationMeetingRequests");
        }
    }
}
