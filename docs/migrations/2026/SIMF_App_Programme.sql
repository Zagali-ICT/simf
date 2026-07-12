/* =====================================================================
   SIMF_App — programme seed  (Hall · Programme days · Sessions)
                              -> GET /app/halls · /app/programme · /app/sessions

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded by IF NOT EXISTS
                     (hall by Code, days by Date, sessions by Code).
   Transactional   : the whole script is one transaction with
                     SET XACT_ABORT ON, so ANY error rolls the whole thing
                     back — no partial data.

   Run ORDER       : run this BEFORE SIMF_App_SeedGaps.sql — that file's
                     booths + venue-map nodes reference Halls.Code = 'MAIN',
                     which this file creates.

   Provenance      : ported verbatim from DefaultContentSeeder (D-681); the
                     content-seeding lane moved from C# to this SQL file
                     (D-718 / owner rule — see README).

   >>> PLACEHOLDER content — replace with the client's real 2026 programme.
       The five sessions and the three November-20-22 days are DEMO rows the
       app renders so it is not empty; the opening session's live URL is a
       policy-valid placeholder an admin replaces via the Control Panel.
       NOTE: the public landing hero advertises 23-25 Nov 2026 — the real
       agenda must reconcile the dates when it lands.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

/* ---------------------------------------------------------------------
   1) MAIN HALL  ->  GET /app/halls
   The single main hall. Capacity/Purpose/SeatSelectionMode mirror the
   DefaultContentSeeder defaults (Purpose = 0, SeatSelectionMode = 0).
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Halls WHERE Code = N'MAIN')
    INSERT INTO dbo.Halls (Id, Code, Name, NameArabic, Capacity,
        Purpose, SeatSelectionMode, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'MAIN', N'Main Hall', N'القاعة الرئيسية', 500,
        0, 0, 1, @now, @sys);

DECLARE @hallId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Halls WHERE Code = N'MAIN');

/* ---------------------------------------------------------------------
   2) PROGRAMME DAYS  ->  GET /app/programme
   Three days (20-22 Nov 2026). Guarded by Date.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE Date = '2026-11-20')
    INSERT INTO dbo.ProgrammeDays (Id, Date, Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-20', N'Day One', N'اليوم الأول', 0, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE Date = '2026-11-21')
    INSERT INTO dbo.ProgrammeDays (Id, Date, Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-21', N'Day Two', N'اليوم الثاني', 1, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE Date = '2026-11-22')
    INSERT INTO dbo.ProgrammeDays (Id, Date, Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-22', N'Day Three', N'اليوم الثالث', 2, 1, @now, @sys);

/* ---------------------------------------------------------------------
   3) SESSIONS  ->  GET /app/sessions
   Five placeholder sessions across the three days. Times are event-local
   (UTC+3). Status = 0, PublishedAt NULL — mirrors the seeder exactly.
   The opening session carries a policy-valid PLACEHOLDER live URL an admin
   replaces via the Control Panel.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WHERE Code = N'S-D1-01')
    INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, HallId, StartUtc, EndUtc, LiveStreamUrl, Status, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'S-D1-01', N'Opening Session', N'الجلسة الافتتاحية', @hallId,
        '2026-11-20T09:00:00+03:00', '2026-11-20T10:00:00+03:00',
        N'https://www.youtube.com/watch?v=dQw4w9WgXcQ', 0, 1, @now, @sys); -- PLACEHOLDER live URL

IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WHERE Code = N'S-D1-02')
    INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, HallId, StartUtc, EndUtc, LiveStreamUrl, Status, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'S-D1-02', N'Panel Session', N'جلسة حوارية', @hallId,
        '2026-11-20T11:00:00+03:00', '2026-11-20T12:30:00+03:00', NULL, 0, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WHERE Code = N'S-D2-01')
    INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, HallId, StartUtc, EndUtc, LiveStreamUrl, Status, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'S-D2-01', N'Morning Session', N'الجلسة الصباحية', @hallId,
        '2026-11-21T09:00:00+03:00', '2026-11-21T10:30:00+03:00', NULL, 0, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WHERE Code = N'S-D2-02')
    INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, HallId, StartUtc, EndUtc, LiveStreamUrl, Status, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'S-D2-02', N'Afternoon Session', N'الجلسة المسائية', @hallId,
        '2026-11-21T11:00:00+03:00', '2026-11-21T12:30:00+03:00', NULL, 0, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Sessions WHERE Code = N'S-D3-01')
    INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, HallId, StartUtc, EndUtc, LiveStreamUrl, Status, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'S-D3-01', N'Closing Session', N'الجلسة الختامية', @hallId,
        '2026-11-22T09:00:00+03:00', '2026-11-22T10:30:00+03:00', NULL, 0, 1, @now, @sys);

COMMIT TRANSACTION;
