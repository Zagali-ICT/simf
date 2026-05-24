using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddAccountStateMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonArabic",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StateChangedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StateChangedByUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AccountState_StateChangedAt",
                table: "AspNetUsers",
                columns: new[] { "AccountState", "StateChangedAt" });

            // P10 — D-051: back-fill the new column for every existing
            // row so the "recently rejected" admin query produces sane
            // answers from the moment the migration lands. CreatedAt is
            // the closest existing timestamp.
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers
                SET    StateChangedAt = CreatedAt
                WHERE  StateChangedAt IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AccountState_StateChangedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RejectionReasonArabic",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StateChangedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StateChangedByUserId",
                table: "AspNetUsers");
        }
    }
}
