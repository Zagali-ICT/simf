using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D232_AddSessionRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordingContentType",
                table: "Sessions",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingFileName",
                table: "Sessions",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecordingSizeBytes",
                table: "Sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingStoredFileName",
                table: "Sessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RecordingUploadedAt",
                table: "Sessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecordingUploadedByUserId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordingContentType",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingFileName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingSizeBytes",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingStoredFileName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingUploadedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingUploadedByUserId",
                table: "Sessions");
        }
    }
}
