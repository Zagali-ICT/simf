using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D248_AddBusinessMeetings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Halls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HallAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    UnitCount = table.Column<int>(type: "int", nullable: true),
                    RowColumnSpec = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HallAllocations_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RowLabel = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    ColumnNumber = table.Column<int>(type: "int", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingTables_Halls_HallId",
                        column: x => x.HallId,
                        principalTable: "Halls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingTableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MeetingType = table.Column<int>(type: "int", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ScheduledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMeetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessMeetings_MeetingTables_MeetingTableId",
                        column: x => x.MeetingTableId,
                        principalTable: "MeetingTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessMeetingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMeetingParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessMeetingParticipants_BusinessMeetings_BusinessMeetingId",
                        column: x => x.BusinessMeetingId,
                        principalTable: "BusinessMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessMeetingParticipants_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_BusinessMeetingId",
                table: "BusinessMeetingParticipants",
                column: "BusinessMeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_CompanyId",
                table: "BusinessMeetingParticipants",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetingParticipants_VisitorUserId",
                table: "BusinessMeetingParticipants",
                column: "VisitorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetings_MeetingTableId_Status",
                table: "BusinessMeetings",
                columns: new[] { "MeetingTableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMeetings_Status_StartUtc",
                table: "BusinessMeetings",
                columns: new[] { "Status", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HallAllocations_HallId_Purpose_ReleasedAt",
                table: "HallAllocations",
                columns: new[] { "HallId", "Purpose", "ReleasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTables_HallId_Code",
                table: "MeetingTables",
                columns: new[] { "HallId", "Code" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingTables_HallId_IsActive",
                table: "MeetingTables",
                columns: new[] { "HallId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessMeetingParticipants");

            migrationBuilder.DropTable(
                name: "HallAllocations");

            migrationBuilder.DropTable(
                name: "BusinessMeetings");

            migrationBuilder.DropTable(
                name: "MeetingTables");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Halls");
        }
    }
}
