using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D426_AddExhibitorVisitorScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExhibitorVisitorScans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExhibitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VisitorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExhibitorVisitorScans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId",
                table: "ExhibitorVisitorScans",
                column: "ExhibitorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId",
                table: "ExhibitorVisitorScans",
                columns: new[] { "ExhibitorUserId", "VisitorUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExhibitorVisitorScans");
        }
    }
}
