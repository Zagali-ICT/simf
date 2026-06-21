using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D472_AddSessionSummaryReviewApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "SessionSummaries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "SessionSummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewSubmittedAt",
                table: "SessionSummaries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewSubmittedByUserId",
                table: "SessionSummaries",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "SessionSummaries");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "SessionSummaries");

            migrationBuilder.DropColumn(
                name: "ReviewSubmittedAt",
                table: "SessionSummaries");

            migrationBuilder.DropColumn(
                name: "ReviewSubmittedByUserId",
                table: "SessionSummaries");
        }
    }
}
