using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddOperationsToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveVisibility",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    LastChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveVisibility", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationGate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    AutoCloseUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationGate", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ArchiveVisibility",
                columns: new[] { "Id", "IsVisible", "LastChangedAt", "LastChangedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });

            migrationBuilder.InsertData(
                table: "RegistrationGate",
                columns: new[] { "Id", "AutoCloseUtc", "IsOpen", "LastChangedAt", "LastChangedByUserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), null, true, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveVisibility");

            migrationBuilder.DropTable(
                name: "RegistrationGate");
        }
    }
}
