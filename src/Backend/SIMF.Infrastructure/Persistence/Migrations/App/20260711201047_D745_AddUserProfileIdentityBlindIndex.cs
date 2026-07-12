using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D745_AddUserProfileIdentityBlindIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IqamaNumberHash",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdHash",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassportNumberHash",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_IqamaNumberHash",
                table: "UserProfiles",
                column: "IqamaNumberHash",
                unique: true,
                filter: "[IqamaNumberHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_NationalIdHash",
                table: "UserProfiles",
                column: "NationalIdHash",
                unique: true,
                filter: "[NationalIdHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_PassportNumberHash",
                table: "UserProfiles",
                column: "PassportNumberHash",
                unique: true,
                filter: "[PassportNumberHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_IqamaNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_NationalIdHash",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_PassportNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IqamaNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "NationalIdHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PassportNumberHash",
                table: "UserProfiles");
        }
    }
}
