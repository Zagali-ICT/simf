using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>
    /// Replaces the old fixed single-row <c>Ratings</c> table with the dynamic,
    /// config-driven model: RatingTypes → RatingQuestionGroups / RatingQuestions,
    /// and RatingResponses → RatingAnswers. Also adds the once-only dedup column
    /// <c>Session.RatingPromptSentUtc</c> for the end-of-session prompt worker.
    /// <para>FREEZE NOTE (D-110): dropping a table + adding five is a STRUCTURAL
    /// change, not additive — taken pre-handover with owner awareness. The schema
    /// freeze must be re-instated before the production publish / handover.</para>
    /// </summary>
    public partial class D496_DynamicRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RatingPromptSentUtc",
                table: "Sessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RatingTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    HasOverallStars = table.Column<bool>(type: "bit", nullable: false),
                    AllowComment = table.Column<bool>(type: "bit", nullable: false),
                    CommentLabel = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CommentLabelArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RatingQuestionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingQuestionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingQuestionGroups_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OverallStars = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingResponses_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RatingQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingQuestionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TextArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingQuestions_RatingQuestionGroups_RatingQuestionGroupId",
                        column: x => x.RatingQuestionGroupId,
                        principalTable: "RatingQuestionGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RatingQuestions_RatingTypes_RatingTypeId",
                        column: x => x.RatingTypeId,
                        principalTable: "RatingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatingAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatingQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stars = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingAnswers_RatingQuestions_RatingQuestionId",
                        column: x => x.RatingQuestionId,
                        principalTable: "RatingQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RatingAnswers_RatingResponses_RatingResponseId",
                        column: x => x.RatingResponseId,
                        principalTable: "RatingResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RatingAnswers_RatingQuestionId",
                table: "RatingAnswers",
                column: "RatingQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingAnswers_RatingResponseId_RatingQuestionId",
                table: "RatingAnswers",
                columns: new[] { "RatingResponseId", "RatingQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestionGroups_RatingTypeId_IsActive_DisplayOrder",
                table: "RatingQuestionGroups",
                columns: new[] { "RatingTypeId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestions_RatingQuestionGroupId",
                table: "RatingQuestions",
                column: "RatingQuestionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingQuestions_RatingTypeId_IsActive_DisplayOrder",
                table: "RatingQuestions",
                columns: new[] { "RatingTypeId", "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_RatingTypeId",
                table: "RatingResponses",
                column: "RatingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_UserId_RatingTypeId_TargetId",
                table: "RatingResponses",
                columns: new[] { "UserId", "RatingTypeId", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingTypes_Code",
                table: "RatingTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatingTypes_IsActive_DisplayOrder",
                table: "RatingTypes",
                columns: new[] { "IsActive", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RatingAnswers");

            migrationBuilder.DropTable(
                name: "RatingQuestions");

            migrationBuilder.DropTable(
                name: "RatingResponses");

            migrationBuilder.DropTable(
                name: "RatingQuestionGroups");

            migrationBuilder.DropTable(
                name: "RatingTypes");

            migrationBuilder.DropColumn(
                name: "RatingPromptSentUtc",
                table: "Sessions");

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppStars = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ContentStars = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OrganizationStars = table.Column<int>(type: "int", nullable: true),
                    Stars = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueStars = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId",
                table: "Ratings",
                column: "UserId",
                unique: true);
        }
    }
}
