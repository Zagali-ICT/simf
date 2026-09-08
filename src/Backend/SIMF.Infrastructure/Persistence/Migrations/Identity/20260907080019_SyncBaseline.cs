using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.Identity
{
    /// <summary>
    /// The Identity half of the convergence described on the App migration of the
    /// same name: every statement is guarded, so this reaches the model from any of
    /// the schemas the pinned migration id left behind rather than assuming one
    /// starting point.
    /// </summary>
    public partial class SyncBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // An index over columns the model no longer maps: it constrains inserts
            // rather than preserving anything, so it goes. The columns stay.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Permissions_Page_Action' AND object_id = OBJECT_ID(N'[Permissions]'))
BEGIN
    DROP INDEX [IX_Permissions_Page_Action] ON [Permissions];
END
");

            // RETAINED, NOT DROPPED - the owner's standing rule is that nothing is
            // deleted. An earlier normalisation dropped Permissions.Page / Action /
            // DisplayName because PermissionCatalog in code owns all three. They are
            // kept here across 286 live rows, and made NULLABLE instead - which is
            // the part that matters: the permission seeder INSERTs rows without
            // naming them, and a NOT NULL column with no default fails every one of
            // those inserts.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Permissions', 'Page')        IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [Page] nvarchar(200) NULL;
IF COL_LENGTH('dbo.Permissions', 'Action')      IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [Action] nvarchar(200) NULL;
IF COL_LENGTH('dbo.Permissions', 'DisplayName') IS NOT NULL ALTER TABLE dbo.Permissions ALTER COLUMN [DisplayName] nvarchar(400) NULL;
");

            // The Arabic collation. SQL Server will not change a column's collation
            // while an index references it, and whether such an index exists depends
            // on which schema this database started from - so the blocking indexes
            // are discovered at run time, dropped, and rebuilt from their own
            // definitions afterwards rather than from a list written here.
            migrationBuilder.Sql(@"
DECLARE @blocking TABLE (definition nvarchar(max));

INSERT INTO @blocking (definition)
SELECT N'CREATE ' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE ' ELSE N'' END + N'INDEX ' + QUOTENAME(i.name)
     + N' ON [Notifications] ('
     + STUFF((SELECT N',' + QUOTENAME(c.name) + CASE WHEN ic2.is_descending_key = 1 THEN N' DESC' ELSE N'' END
              FROM sys.index_columns ic2
              JOIN sys.columns c ON c.object_id = ic2.object_id AND c.column_id = ic2.column_id
              WHERE ic2.object_id = i.object_id AND ic2.index_id = i.index_id AND ic2.is_included_column = 0
              ORDER BY ic2.key_ordinal FOR XML PATH('')), 1, 1, N'')
     + N')' + ISNULL(N' WHERE ' + i.filter_definition, N'') + N';'
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(N'[Notifications]') AND i.name IS NOT NULL
  AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
  AND EXISTS (SELECT 1 FROM sys.index_columns ic
              JOIN sys.columns c2 ON c2.object_id = ic.object_id AND c2.column_id = ic.column_id
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                AND c2.name IN (N'TitleArabic', N'BodyArabic'));

DECLARE @name sysname, @sql nvarchar(max);
DECLARE dropper CURSOR LOCAL FAST_FORWARD FOR
    SELECT i.name FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'[Notifications]') AND i.name IS NOT NULL
      AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
      AND EXISTS (SELECT 1 FROM sys.index_columns ic
                  JOIN sys.columns c2 ON c2.object_id = ic.object_id AND c2.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND c2.name IN (N'TitleArabic', N'BodyArabic'));
OPEN dropper;
FETCH NEXT FROM dropper INTO @name;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'DROP INDEX ' + QUOTENAME(@name) + N' ON [Notifications];';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM dropper INTO @name;
END
CLOSE dropper;
DEALLOCATE dropper;

DECLARE @dc sysname;
SELECT @dc = QUOTENAME(d.name) FROM sys.default_constraints d
JOIN sys.columns c ON c.column_id = d.parent_column_id AND c.object_id = d.parent_object_id
WHERE d.parent_object_id = OBJECT_ID(N'[Notifications]') AND c.name = N'TitleArabic';
IF @dc IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT ' + @dc + N';');
IF COL_LENGTH(N'[Notifications]', N'TitleArabic') IS NOT NULL
    ALTER TABLE [Notifications] ALTER COLUMN [TitleArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;

SET @dc = NULL;
SELECT @dc = QUOTENAME(d.name) FROM sys.default_constraints d
JOIN sys.columns c ON c.column_id = d.parent_column_id AND c.object_id = d.parent_object_id
WHERE d.parent_object_id = OBJECT_ID(N'[Notifications]') AND c.name = N'BodyArabic';
IF @dc IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT ' + @dc + N';');
IF COL_LENGTH(N'[Notifications]', N'BodyArabic') IS NOT NULL
    ALTER TABLE [Notifications] ALTER COLUMN [BodyArabic] nvarchar(2000) COLLATE Arabic_CI_AI NOT NULL;

DECLARE @recreate nvarchar(max);
DECLARE rebuilder CURSOR LOCAL FAST_FORWARD FOR SELECT definition FROM @blocking;
OPEN rebuilder;
FETCH NEXT FROM rebuilder INTO @recreate;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sp_executesql @recreate;
    FETCH NEXT FROM rebuilder INTO @recreate;
END
CLOSE rebuilder;
DEALLOCATE rebuilder;
");

            // The three check constraints, each skipped where it already exists.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Notifications_GroupCode' AND parent_object_id = OBJECT_ID(N'[Notifications]'))
    ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_GroupCode] CHECK ([GroupCode] IN ('Account', 'Vip', 'Bookings', 'Sessions', 'Meetings', 'Ratings'));
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_DeviceKeys_ChallengePin' AND parent_object_id = OBJECT_ID(N'[DeviceKeys]'))
    ALTER TABLE [DeviceKeys] ADD CONSTRAINT [CK_DeviceKeys_ChallengePin] CHECK (([CurrentChallenge] IS NULL AND [ChallengeExpiresAt] IS NULL) OR ([CurrentChallenge] IS NOT NULL AND [ChallengeExpiresAt] IS NOT NULL));
");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_AccountCodes_OneOwner' AND parent_object_id = OBJECT_ID(N'[AccountCodes]'))
    ALTER TABLE [AccountCodes] ADD CONSTRAINT [CK_AccountCodes_OneOwner] CHECK (([UserId] IS NOT NULL AND [UserProfileId] IS NULL) OR ([UserId] IS NULL AND [UserProfileId] IS NOT NULL));
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// NOT REVERSIBLE. Up() retains Permissions.Page / Action / DisplayName
        /// rather than dropping them, so EF's generated Down() - which re-adds all
        /// three - would fail with a duplicate-column error. Restore from the backup
        /// taken before this migration was applied.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "20260907080019_SyncBaseline cannot be reverted. It retains columns "
                + "EF would have dropped, so the generated Down() would re-add "
                + "columns that still exist. Restore from backup.");
    }
}
