using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D456_AddArchivePastSpeakerCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CountryId",
                table: "ArchivePastSpeakers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_CountryId",
                table: "ArchivePastSpeakers",
                column: "CountryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivePastSpeakers_Countries_CountryId",
                table: "ArchivePastSpeakers",
                column: "CountryId",
                principalTable: "Countries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchivePastSpeakers_Countries_CountryId",
                table: "ArchivePastSpeakers");

            migrationBuilder.DropIndex(
                name: "IX_ArchivePastSpeakers_CountryId",
                table: "ArchivePastSpeakers");

            migrationBuilder.DropColumn(
                name: "CountryId",
                table: "ArchivePastSpeakers");
        }
    }
}
