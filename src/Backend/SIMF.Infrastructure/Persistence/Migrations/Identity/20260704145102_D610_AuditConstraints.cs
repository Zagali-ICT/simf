using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class D610_AuditConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TotpRecoveryCodes_UserId",
                table: "TotpRecoveryCodes");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_TotpRecoveryCodes_UserId_CodeHash",
                table: "TotpRecoveryCodes",
                columns: new[] { "UserId", "CodeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceKeys_UserId_PublicKey",
                table: "DeviceKeys",
                columns: new[] { "UserId", "PublicKey" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType_CreatedAt",
                table: "AspNetUsers",
                columns: new[] { "UserType", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AspNetUsers_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AspNetUsers_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_TotpRecoveryCodes_UserId_CodeHash",
                table: "TotpRecoveryCodes");

            migrationBuilder.DropIndex(
                name: "IX_DeviceKeys_UserId_PublicKey",
                table: "DeviceKeys");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType_CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_TotpRecoveryCodes_UserId",
                table: "TotpRecoveryCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers",
                column: "UserType");
        }
    }
}
