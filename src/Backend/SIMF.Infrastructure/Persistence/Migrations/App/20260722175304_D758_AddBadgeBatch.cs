using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D758_AddBadgeBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BadgeBatchId",
                table: "UserProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BadgeBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountsSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    IsDelegate = table.Column<bool>(type: "bit", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_BadgeBatchId",
                table: "UserProfiles",
                column: "BadgeBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatches_IsActive_CreatedAt",
                table: "BadgeBatches",
                columns: new[] { "IsActive", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_BadgeBatches_BadgeBatchId",
                table: "UserProfiles",
                column: "BadgeBatchId",
                principalTable: "BadgeBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_BadgeBatches_BadgeBatchId",
                table: "UserProfiles");

            migrationBuilder.DropTable(
                name: "BadgeBatches");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_BadgeBatchId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "BadgeBatchId",
                table: "UserProfiles");
        }
    }
}
