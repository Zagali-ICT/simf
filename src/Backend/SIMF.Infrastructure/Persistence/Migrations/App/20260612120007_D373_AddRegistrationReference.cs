using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D373_AddRegistrationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber",
                table: "UserProfiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ReferenceNumber",
                table: "UserProfiles",
                column: "ReferenceNumber",
                unique: true,
                filter: "[ReferenceNumber] IS NOT NULL");

            // D-373 — the concurrency-safe issuer for the human-quotable
            // registration reference (SIMF-<year>-<value:D8>).
            migrationBuilder.Sql(
                "CREATE SEQUENCE [dbo].[RegistrationReferenceSequence] AS bigint START WITH 1 INCREMENT BY 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP SEQUENCE [dbo].[RegistrationReferenceSequence];");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_ReferenceNumber",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber",
                table: "UserProfiles");
        }
    }
}
