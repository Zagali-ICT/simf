using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddDelegationAvailabilityWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DelegationAvailabilityWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DelegationAvailabilityWindows", x => x.Id);
                    table.CheckConstraint("CK_DelegationAvailabilityWindows_TimeWindow", "[EndUtc] > [StartUtc]");
                    table.ForeignKey(
                        name: "FK_DelegationAvailabilityWindows_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_IsActive_StartUtc",
                table: "DelegationAvailabilityWindows",
                columns: new[] { "CountryId", "IsActive", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DelegationAvailabilityWindows_CountryId_StartUtc",
                table: "DelegationAvailabilityWindows",
                columns: new[] { "CountryId", "StartUtc" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DelegationAvailabilityWindows");
        }
    }
}
