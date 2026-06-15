using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class D429_AddVipProfileExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Honorific",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MawjId",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "UserProfiles",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VipPhotoRelativePath",
                table: "UserProfiles",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Honorific",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "MawjId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "VipPhotoRelativePath",
                table: "UserProfiles");
        }
    }
}
