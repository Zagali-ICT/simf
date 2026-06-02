using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D237_AddSessionSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyPoints = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    KeyPointsArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Recommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecommendationsArabic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Speakers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SpeakersArabic = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FullText = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    FullTextArabic = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    AiModel = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionSummaries_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_IsActive_PublishedAt",
                table: "SessionSummaries",
                columns: new[] { "IsActive", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_SessionId",
                table: "SessionSummaries",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionSummaries");
        }
    }
}
