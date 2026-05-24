using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <summary>
    /// P8 (D-049) — single architectural change: rename
    /// <c>VisitorProfiles</c> table to <c>UserProfiles</c>; drop the
    /// legacy <c>VisitorType</c> string column; move the
    /// <c>ProfileTypeId</c> FK from <c>AspNetUsers</c> onto the renamed
    /// table; back-fill any P7a / P7d assignments so an admin who
    /// previously picked a profile-type for an Other / Visitor user
    /// keeps it through the cut-over.
    /// </summary>
    public partial class RenameVisitorProfileToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop the FK + index on the old AspNetUsers.ProfileTypeId
            //    column so the column can be dropped at the end.
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ProfileTypes_ProfileTypeId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProfileTypeId",
                table: "AspNetUsers");

            // 2. Rename the table (sp_rename — preserves every row).
            migrationBuilder.RenameTable(
                name: "VisitorProfiles",
                newName: "UserProfiles");

            // The PK and the unique index follow the table physically;
            // rename them so the names stay consistent with the new table.
            migrationBuilder.RenameIndex(
                name: "IX_VisitorProfiles_UserId",
                table: "UserProfiles",
                newName: "IX_UserProfiles_UserId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'PK_VisitorProfiles', N'PK_UserProfiles', N'OBJECT';");

            // 3. Drop the legacy VisitorType string column (it was the
            //    self-declared "Visitor / Exhibitor / Press"; replaced by
            //    the ProfileTypeId FK going forward).
            migrationBuilder.DropColumn(
                name: "VisitorType",
                table: "UserProfiles");

            // 4. Add ProfileTypeId on the renamed table.
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileTypeId",
                table: "UserProfiles",
                type: "uniqueidentifier",
                nullable: true);

            // 5. Back-fill: any existing UserProfile row gets the
            //    ProfileTypeId that lived on its owning AspNetUsers row.
            migrationBuilder.Sql(@"
                UPDATE up
                SET    up.ProfileTypeId = u.ProfileTypeId
                FROM   UserProfiles up
                       INNER JOIN AspNetUsers u ON u.Id = up.UserId
                WHERE  u.ProfileTypeId IS NOT NULL;
            ");

            // 6. Stub-row back-fill: P7a / P7d created Other / Visitor
            //    users with a ProfileTypeId on AspNetUsers but **no**
            //    profile row. Create a minimal stub so the FK has
            //    somewhere to land and the user can fill the rest later.
            //    Admins never carry a profile.
            migrationBuilder.Sql(@"
                INSERT INTO UserProfiles
                    (Id, UserId, ProfileTypeId, ArabicName, EnglishName,
                     NationalityCode, PlaceOfBirth, IsSaudi, CreatedAt)
                SELECT NEWID(), u.Id, u.ProfileTypeId, N'', N'', N'SA', N'',
                       0, SYSDATETIMEOFFSET()
                FROM   AspNetUsers u
                WHERE  u.ProfileTypeId IS NOT NULL
                  AND  u.UserType <> 'Admin'
                  AND  NOT EXISTS (SELECT 1 FROM UserProfiles up WHERE up.UserId = u.Id);
            ");

            // 7. Index + FK on the new column.
            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ProfileTypeId",
                table: "UserProfiles",
                column: "ProfileTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserProfiles_ProfileTypes_ProfileTypeId",
                table: "UserProfiles",
                column: "ProfileTypeId",
                principalTable: "ProfileTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 8. Drop the now-orphan AspNetUsers.ProfileTypeId column.
            migrationBuilder.DropColumn(
                name: "ProfileTypeId",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirror image — re-add ProfileTypeId on AspNetUsers,
            // back-fill from UserProfiles, then unwind the rename.
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileTypeId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE u
                SET    u.ProfileTypeId = up.ProfileTypeId
                FROM   AspNetUsers u
                       INNER JOIN UserProfiles up ON up.UserId = u.Id
                WHERE  up.ProfileTypeId IS NOT NULL;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_UserProfiles_ProfileTypes_ProfileTypeId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_ProfileTypeId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ProfileTypeId",
                table: "UserProfiles");

            migrationBuilder.AddColumn<string>(
                name: "VisitorType",
                table: "UserProfiles",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Visitor");

            migrationBuilder.Sql(
                "EXEC sp_rename N'PK_UserProfiles', N'PK_VisitorProfiles', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                newName: "IX_VisitorProfiles_UserId");

            migrationBuilder.RenameTable(
                name: "UserProfiles",
                newName: "VisitorProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProfileTypeId",
                table: "AspNetUsers",
                column: "ProfileTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ProfileTypes_ProfileTypeId",
                table: "AspNetUsers",
                column: "ProfileTypeId",
                principalTable: "ProfileTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
