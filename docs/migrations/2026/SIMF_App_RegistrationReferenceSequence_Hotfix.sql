/*
  SIMF_App — hotfix: create the missing dbo.RegistrationReferenceSequence.

  WHY
    D-373 issues the human-quotable registration reference
    (SIMF-<year>-<8-digit>) from a SQL sequence, read in
    UserProfileRepository.NextRegistrationReferenceAsync via
    `SELECT NEXT VALUE FOR [dbo].[RegistrationReferenceSequence]`, only on a
    FIRST-TIME profile save. That sequence was created by hand on the dev DB and
    never captured in a migration, so the D-743 InitialMigration squash (which
    rebuilt prod after "DROP both DBs") produced a SIMF_App with no sequence.
    Every "create user / save sign-up profile" then threw
    SqlException 208 (Invalid object name 'dbo.RegistrationReferenceSequence')
    -> HTTP 500 -> the app showed the generic "An unexpected error occurred /
    حدث خطأ غير متوقع" with no details.

  WHAT
    1) Idempotently create the sequence (bigint, start 1, increment 1) — this
       unblocks prod IMMEDIATELY with no redeploy.
    2) Idempotently mark the EF migration 20260713080333_AddRegistrationReferenceSequence
       as applied, so the next deploy's startup Migrate() (Program.cs) does NOT
       try to CREATE SEQUENCE a second time and fail.

  HOW
    Run once against the SIMF_App database (SSMS or sqlcmd). Idempotent — safe to
    re-run. The permanent fix lands with the migration on the next deploy; this
    script only bridges the running prod until then.
*/

USE [SIMF_App];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.sequences
    WHERE name = 'RegistrationReferenceSequence' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE SEQUENCE [dbo].[RegistrationReferenceSequence]
        AS bigint
        START WITH 1
        INCREMENT BY 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713080333_AddRegistrationReferenceSequence')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260713080333_AddRegistrationReferenceSequence', N'10.0.8');
END
GO
