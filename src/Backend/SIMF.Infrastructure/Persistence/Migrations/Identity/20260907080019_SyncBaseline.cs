using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <inheritdoc />
    public partial class SyncBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Permissions_Page_Action",
                table: "Permissions");

            // RETAINED, NOT DROPPED - the owner's standing rule is that nothing is deleted.
            // An earlier normalisation dropped Permissions.Page / Action / DisplayName because PermissionCatalog
            // in code is the source of truth for all three. That remains true, and they are
            // still not dropped here: they are NOT NULL with no default across 286 live rows,
            // and the owner's instruction is that nothing is deleted.
            //
            // They are made NULLABLE instead, which is the part that actually matters. The
            // permission seeder INSERTs rows without naming these columns, so a NOT NULL
            // column with no default would fail every one of those inserts - the reason they
            // could not simply be left alone. The values stay, the model ignores them, and
            // they can be dropped in a migration of their own if the owner decides to.
            //
            // IX_Permissions_Page_Action IS dropped above: an index over columns the model no
            // longer maps constrains inserts rather than preserving anything.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Permissions', 'Page')        IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [Page] nvarchar(200) NULL;
IF COL_LENGTH('dbo.Permissions', 'Action')      IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [Action] nvarchar(200) NULL;
IF COL_LENGTH('dbo.Permissions', 'DisplayName') IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [DisplayName] nvarchar(400) NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "Notifications",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "BodyArabic",
                table: "Notifications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_GroupCode",
                table: "Notifications",
                sql: "[GroupCode] IN ('Account', 'Vip', 'Bookings', 'Sessions', 'Meetings', 'Ratings')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DeviceKeys_ChallengePin",
                table: "DeviceKeys",
                sql: "([CurrentChallenge] IS NULL AND [ChallengeExpiresAt] IS NULL) OR ([CurrentChallenge] IS NOT NULL AND [ChallengeExpiresAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountCodes_OneOwner",
                table: "AccountCodes",
                sql: "([UserId] IS NOT NULL AND [UserProfileId] IS NULL) OR ([UserId] IS NULL AND [UserProfileId] IS NOT NULL)");
        }

        /// <inheritdoc />
        /// <remarks>
        /// NOT REVERSIBLE. Up() retains Permissions.Page / Action /
        /// DisplayName rather than dropping them, so EF's generated Down() - which
        /// re-adds all three - would fail with a duplicate-column error. Restore
        /// from the backup taken before this migration was applied.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "20260907080019_SyncBaseline cannot be reverted. It retains columns "
                + "EF would have dropped, so the generated Down() would re-add "
                + "columns that still exist. Restore from backup.");
    }
}
