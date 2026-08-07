/*
  SIMF_App — interim hotfix: create the missing dbo.RegistrationReferenceSequence
  on a RUNNING production database that predates the consolidated migration.

  WHY
    D-373 issues the human-quotable registration reference (SIMF-<year>-<8-digit>)
    from a SQL sequence, read in UserProfileRepository.NextRegistrationReferenceAsync
    via `SELECT NEXT VALUE FOR [dbo].[RegistrationReferenceSequence]`, only on a
    FIRST-TIME profile save. The sequence had only ever been hand-created on the dev
    DB and was never captured in a migration, so a running prod whose schema predates
    the consolidated migration lacks it: every "create user / save sign-up profile"
    then threw SqlException 208 (Invalid object name
    'dbo.RegistrationReferenceSequence') -> HTTP 500 -> the app showed the generic
    "An unexpected error occurred / حدث خطأ غير متوقع" with no details.

  THE PERMANENT FIX (not this script)
    The sequence is now declared on the model (SimfAppDbContext.OnModelCreating,
    HasSequence<long>) and is part of the single consolidated App migration
    `<ts>_20260712001`, so a FRESH database (drop + MigrateAsync) creates it
    automatically. Because that consolidation changes the migration id, the branch
    deploys by rebuilding the database (drop + recreate), exactly like the D-743
    "DROP both DBs" deploy — a fresh DB needs nothing from this file.

  WHEN TO USE THIS
    ONLY to un-block a CURRENTLY-RUNNING prod that is missing the sequence and that
    you are NOT rebuilding right now (no redeploy). It creates just the sequence so
    create-user works immediately. Idempotent — safe to re-run.

  WHICH DATABASE
    Run against the APP database — the one the API's `SimfAppDb` connection string
    points to (NOT the Identity DB, NOT master). Its name comes from the prod env
    var, so it may not be literally "SIMF_App". Find it with STEP 0, then select it
    in SSMS (or `sqlcmd -d <name>`) and run STEP 1. Do NOT add a `USE [...]`.
*/

-- STEP 0 — find the App database (run against master). The App DB is the one
-- that contains BOTH [__EFMigrationsHistory_App] and the App tables (e.g. UserProfiles).
-- SELECT name FROM sys.databases ORDER BY name;
-- Then select that database and run STEP 1.

-- ── STEP 1 — create the sequence (the interim unblock) ───────────────────────
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
