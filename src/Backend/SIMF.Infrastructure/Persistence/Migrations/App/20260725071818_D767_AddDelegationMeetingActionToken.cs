using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D767_AddDelegationMeetingActionToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DelegationMeetingActionTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegationMeetingRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DelegationMeetingActionTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DelegationMeetingActionTokens_DelegationMeetingRequests_DelegationMeetingRequestId",
                        column: x => x.DelegationMeetingRequestId,
                        principalTable: "DelegationMeetingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingActionTokens_DelegationMeetingRequestId",
                table: "DelegationMeetingActionTokens",
                column: "DelegationMeetingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingActionTokens_TokenHash",
                table: "DelegationMeetingActionTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationMeetingActionTokens");
        }
    }
}
