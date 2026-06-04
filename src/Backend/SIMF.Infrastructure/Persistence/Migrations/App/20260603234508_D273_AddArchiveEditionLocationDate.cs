using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D273_AddArchiveEditionLocationDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DateLabelAr",
                table: "ArchiveEditions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DateLabelEn",
                table: "ArchiveEditions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationAr",
                table: "ArchiveEditions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationEn",
                table: "ArchiveEditions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateLabelAr",
                table: "ArchiveEditions");

            migrationBuilder.DropColumn(
                name: "DateLabelEn",
                table: "ArchiveEditions");

            migrationBuilder.DropColumn(
                name: "LocationAr",
                table: "ArchiveEditions");

            migrationBuilder.DropColumn(
                name: "LocationEn",
                table: "ArchiveEditions");
        }
    }
}
