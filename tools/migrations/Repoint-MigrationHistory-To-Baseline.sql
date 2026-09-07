-- Re-point both migration-history rows at the pinned baseline.
--
-- This changes NO schema and NO data. It relabels a row that is already true:
-- 00000000000000_InitialCreate was proved to describe these two databases
-- exactly (a sqlpackage diff against both emitted zero statements). After this,
-- `dotnet ef database update` applies only SyncBaseline.
--
-- Pre-change backups already taken:
--   ...\MSSQL\Backup\SIMF_App_preD959.bak
--   ...\MSSQL\Backup\SIMF_Identity_preD959.bak
SET NOCOUNT ON;

UPDATE SIMF_App.dbo.__EFMigrationsHistory_App
   SET MigrationId = N'00000000000000_InitialCreate'
 WHERE MigrationId = N'20260814115348_InitialCreate';
PRINT 'App rows re-pointed: ' + CAST(@@ROWCOUNT AS varchar(10));

UPDATE SIMF_Identity.dbo.__EFMigrationsHistory_Identity
   SET MigrationId = N'00000000000000_InitialCreate'
 WHERE MigrationId = N'20260814115334_InitialCreate';
PRINT 'Identity rows re-pointed: ' + CAST(@@ROWCOUNT AS varchar(10));

SELECT 'App' AS ctx, MigrationId FROM SIMF_App.dbo.__EFMigrationsHistory_App
UNION ALL
SELECT 'Identity', MigrationId FROM SIMF_Identity.dbo.__EFMigrationsHistory_Identity;
