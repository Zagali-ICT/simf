using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddSnapshotColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayName",
                schema: "app",
                table: "RowAudits",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActorDisplayName",
                table: "OperationLog",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectDisplayName",
                table: "OperationLog",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScannedDisplayName",
                table: "GateScans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScannedProfileTypeName",
                table: "GateScans",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorDisplayName",
                schema: "app",
                table: "RowAudits");

            migrationBuilder.DropColumn(
                name: "ActorDisplayName",
                table: "OperationLog");

            migrationBuilder.DropColumn(
                name: "SubjectDisplayName",
                table: "OperationLog");

            migrationBuilder.DropColumn(
                name: "ScannedDisplayName",
                table: "GateScans");

            migrationBuilder.DropColumn(
                name: "ScannedProfileTypeName",
                table: "GateScans");
        }
    }
}
