using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddUserTypeAndProfileType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileTypeId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserType",
                table: "AspNetUsers",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                // P7 data-migration rule (decision D-048):
                // every existing row defaults to "Visitor" (the safe
                // least-privileged bucket); the UPDATE below promotes
                // Administrator-role holders to "Admin". Anyone who is
                // actually an Other gets reclassified manually after
                // deployment via the CP user-management page.
                defaultValue: "Visitor");

            // Promote every existing Administrator to UserType = Admin.
            // The role-name lookup is by string so this still works if
            // the Administrator row's Guid was generated differently
            // between environments.
            migrationBuilder.Sql(@"
                UPDATE u
                SET    u.UserType = 'Admin'
                FROM   AspNetUsers u
                       INNER JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                       INNER JOIN AspNetRoles r      ON r.Id     = ur.RoleId
                WHERE  r.Name = 'Administrator';
            ");

            migrationBuilder.CreateTable(
                name: "ProfileTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NameArabic = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PageColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfileTypeId",
                table: "AspNetUsers",
                column: "ProfileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers",
                column: "UserType");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileTypes_UserType_IsActive",
                table: "ProfileTypes",
                columns: new[] { "UserType", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ProfileTypes_ProfileTypeId",
                table: "AspNetUsers",
                column: "ProfileTypeId",
                principalTable: "ProfileTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ProfileTypes_ProfileTypeId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ProfileTypes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfileTypeId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ProfileTypeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserType",
                table: "AspNetUsers");
        }
    }
}
