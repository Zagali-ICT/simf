using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddAiModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromptKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TokensInput = table.Column<int>(type: "int", nullable: true),
                    TokensOutput = table.Column<int>(type: "int", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CallerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CallerKind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayNameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescriptionArabic = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SystemPrompt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPromptTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPrompts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_CallerUserId_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "CallerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_CreatedAt",
                table: "AiInvocations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_ErrorCode_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "ErrorCode", "CreatedAt" },
                filter: "[ErrorCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AiInvocations_Feature_CreatedAt",
                table: "AiInvocations",
                columns: new[] { "Feature", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiPrompts_Feature_IsActive",
                table: "AiPrompts",
                columns: new[] { "Feature", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AiPrompts_Key",
                table: "AiPrompts",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiInvocations");

            migrationBuilder.DropTable(
                name: "AiPrompts");
        }
    }
}
