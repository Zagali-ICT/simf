using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class D818_RenameUtcColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "PasswordHistory",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordHistory_UserId_CreatedAtUtc",
                table: "PasswordHistory",
                newName: "IX_PasswordHistory_UserId_CreatedAt");

            migrationBuilder.RenameColumn(
                name: "PasswordChangedAtUtc",
                table: "AspNetUsers",
                newName: "PasswordChangedAt");

            migrationBuilder.RenameColumn(
                name: "LastSuccessfulSignInAtUtc",
                table: "AspNetUsers",
                newName: "LastSuccessfulSignInAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "PasswordHistory",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_PasswordHistory_UserId_CreatedAt",
                table: "PasswordHistory",
                newName: "IX_PasswordHistory_UserId_CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "PasswordChangedAt",
                table: "AspNetUsers",
                newName: "PasswordChangedAtUtc");

            migrationBuilder.RenameColumn(
                name: "LastSuccessfulSignInAt",
                table: "AspNetUsers",
                newName: "LastSuccessfulSignInAtUtc");
        }
    }
}
