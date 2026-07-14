/* =====================================================================
   SIMF_App — 2026 content-seed MASTER RUNNER  (SSMS · SQLCMD Mode)

   Runs every content-seed file in this folder, in the required order, in
   one execution, against the App database. Each seed is idempotent and wrapped
   in its own SET XACT_ABORT transaction; this runner adds :on error exit, so
   the FIRST failure stops the run (no partial data, later seeds do not run).
   Safe to re-run.

   ── HOW TO RUN ───────────────────────────────────────────────────────────
   1) Open this file in SSMS.
   2) Turn ON SQLCMD Mode:  Query menu -> "SQLCMD Mode"   (REQUIRED — without it
      the :setvar / :r / :on lines below fail with "Incorrect syntax near ':'").
   3) Edit the two :setvar values just below:
        MigrationDir = the FULL path to THIS folder (…\docs\migrations\2026).
        AppDb        = the App / CONTENT database — the one that holds the app
                       tables (dbo.Speakers, dbo.Halls, dbo.Sessions, …). The
                       physical name is environment-specific (it is whatever the
                       API's SimfAppDb connection string points at): on the server
                       it is  SIMF_Data ; on a local dev box it is  SIMF_App . It is
                       NEVER the Identity database (SIMF_Identity / SIMF_Data_Identity).
                       Not sure which one? Run this on the server and pick the hit:
                         SELECT name FROM sys.databases
                         WHERE OBJECT_ID(QUOTENAME(name)+'.dbo.Speakers') IS NOT NULL;
   4) Execute (F5). The Messages tab shows [1/9]…[9/9] progress.

   ── NOT run by this file ──────────────────────────────────────────────────
   • SIMF_App_RegistrationReferenceSequence_Hotfix.sql — a SEPARATE, prod-ONLY
     unblock for a running DB that is missing the sequence. Run it by hand only
     if that is your situation (see its own header). It is NOT content seeding.
   • The speaker-photo BYTES — AFTER this runs, copy the image folder
        speaker-photos\speakerphoto  ->  <FileStorage:RootPath>\speakerphoto
     (production: C:\SIMF\Storage\files\speakerphoto). A StoredFile row with no
     file on disk simply 404s the photo (see README / speaker-photos\MANIFEST.txt).

   ── Order matters ─────────────────────────────────────────────────────────
   Programme creates the MAIN hall that SeedGaps (booths + venue-map) references,
   so Programme runs BEFORE SeedGaps; SpeakerPhotos points at the rows Speakers
   creates, so Speakers runs BEFORE SpeakerPhotos.

   NOTE: the seed files carry no USE and no GO of their own (each is one batch),
   so this runner sets the database once (USE below) and puts a GO after every
   :r — that keeps each seed in its own batch (they each DECLARE @sys/@now).
   ===================================================================== */

:on error exit
:setvar MigrationDir "D:\SIMF\System\V1.0.0\docs\migrations\2026"
:setvar AppDb        "SIMF_Data"
-- ^ the App/CONTENT DB (holds dbo.Speakers/Halls). Server: SIMF_Data · local
--   dev: SIMF_App. NEVER the Identity DB. See the discovery query in the header.

SET NOCOUNT ON;
GO
USE [$(AppDb)];
GO
PRINT '=== SIMF_App 2026 content seed — running on [' + DB_NAME() + '] ===';
GO

PRINT '--- [1/9] Programme  (hall - days - sessions) ---';
GO
:r $(MigrationDir)\SIMF_App_Programme.sql
GO
PRINT '--- [2/9] News ---';
GO
:r $(MigrationDir)\SIMF_App_News.sql
GO
PRINT '--- [3/9] Sponsors ---';
GO
:r $(MigrationDir)\SIMF_App_Sponsors.sql
GO
PRINT '--- [4/9] Media partners ---';
GO
:r $(MigrationDir)\SIMF_App_MediaPartners.sql
GO
PRINT '--- [5/9] Archive editions ---';
GO
:r $(MigrationDir)\SIMF_App_Archive.sql
GO
PRINT '--- [6/9] Organisation  (about - vision - social) ---';
GO
:r $(MigrationDir)\SIMF_App_Organization.sql
GO
PRINT '--- [7/9] Speakers ---';
GO
:r $(MigrationDir)\SIMF_App_Speakers.sql
GO
PRINT '--- [8/9] Speaker photos  (StoredFile rows) ---';
GO
:r $(MigrationDir)\SIMF_App_SpeakerPhotos.sql
GO
PRINT '--- [9/9] SeedGaps  (booths - delegations - FAQ - venue map) ---';
GO
:r $(MigrationDir)\SIMF_App_SeedGaps.sql
GO

PRINT '=== content seed COMPLETE on [' + DB_NAME() + '] ===';
PRINT 'Next: copy  speaker-photos\speakerphoto  ->  <FileStorage:RootPath>\speakerphoto';
GO
