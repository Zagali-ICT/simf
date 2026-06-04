using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D211_AddFaq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaqGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaqGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QuestionAr = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AnswerEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AnswerAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaqEntries_FaqGroups_FaqGroupId",
                        column: x => x.FaqGroupId,
                        principalTable: "FaqGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaqEntries_FaqGroupId_IsActive_DisplayOrder",
                table: "FaqEntries",
                columns: new[] { "FaqGroupId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FaqGroups_IsActive_DisplayOrder",
                table: "FaqGroups",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaqEntries");

            migrationBuilder.DropTable(
                name: "FaqGroups");
        }
    }
}
