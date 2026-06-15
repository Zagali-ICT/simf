using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D432_AddArchiveDetailLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveMediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CaptionEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CaptionAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveMediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveMediaItems_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchivePastSpeakers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PhotoRelativePath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivePastSpeakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivePastSpeakers_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveSessionTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchiveEditionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveSessionTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveSessionTitles_ArchiveEditions_ArchiveEditionId",
                        column: x => x.ArchiveEditionId,
                        principalTable: "ArchiveEditions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveMediaItems_ArchiveEditionId_DisplayOrder",
                table: "ArchiveMediaItems",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_ArchiveEditionId_DisplayOrder",
                table: "ArchivePastSpeakers",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveSessionTitles_ArchiveEditionId_DisplayOrder",
                table: "ArchiveSessionTitles",
                columns: new[] { "ArchiveEditionId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveMediaItems");

            migrationBuilder.DropTable(
                name: "ArchivePastSpeakers");

            migrationBuilder.DropTable(
                name: "ArchiveSessionTitles");
        }
    }
}
