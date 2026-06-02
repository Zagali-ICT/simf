using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D233_AddSessionQuestionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiFilterVerdict",
                table: "SessionQuestions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToRole",
                table: "SessionQuestions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EscalatedAt",
                table: "SessionQuestions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedByUserId",
                table: "SessionQuestions",
                type: "uniqueidentifier",
                nullable: true);

            // Existing rows are historical live questions.
            migrationBuilder.AddColumn<int>(
                name: "Phase",
                table: "SessionQuestions",
                type: "int",
                nullable: false,
                defaultValue: 1); // Live

            // Backfill existing rows to Approved so the (now Status-aware) moderator
            // desk keeps showing them; new rows are inserted Pending by EF. The
            // SQL below then marks the previously-hidden ones Hidden.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SessionQuestions",
                type: "int",
                nullable: false,
                defaultValue: 1); // Approved

            migrationBuilder.Sql(
                "UPDATE [SessionQuestions] SET [Status] = 2 WHERE [IsHidden] = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_SessionId_Status_Order",
                table: "SessionQuestions",
                columns: new[] { "SessionId", "Status", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_Status_CreatedAt",
                table: "SessionQuestions",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_SessionId_Status_Order",
                table: "SessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_Status_CreatedAt",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "AiFilterVerdict",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "AssignedToRole",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "EscalatedByUserId",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "Phase",
                table: "SessionQuestions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SessionQuestions");
        }
    }
}
