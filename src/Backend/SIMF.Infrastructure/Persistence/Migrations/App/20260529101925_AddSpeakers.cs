using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddSpeakers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Speakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BioArabic = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Qualifications = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    TrainingExperience = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Awards = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PhotoRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Speakers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_Code",
                table: "Speakers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_IsActive_DisplayOrder",
                table: "Speakers",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Speakers");
        }
    }
}
