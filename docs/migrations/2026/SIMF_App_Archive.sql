/* =====================================================================
   SIMF_App — archive (past editions) seed  ->  GET /app/archive

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — editions guarded by Year; the 2024 child lists
                     (session titles + past speakers) only seed when that
                     edition currently has none.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder.EnsureDemoArchiveEditions
                     (D-347); the content-seeding lane moved from C# to this SQL
                     file (D-718 / owner rule — see README).

   >>> PLACEHOLDER content — four past-edition rows (2022-2025) with demo
       counters, plus rich child lists on 2024 (Figma 925-3079). Replace with
       the client's real archive. Past-speaker photos / edition media are
       uploaded via the Control Panel (StoredFile); none are shipped here.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)
DECLARE @saudi int = 682; -- Country.Id (ISO-3166 numeric) for Saudi Arabia

/* ---------------------------------------------------------------------
   1) PAST EDITIONS  (guarded by Year)
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveEditions WHERE Year = 2022)
    INSERT INTO dbo.ArchiveEditions (Id, Year, TitleEn, TitleAr, SummaryEn, SummaryAr,
        LocationEn, LocationAr, DateLabelEn, DateLabelAr, Attendees, Sessions, Speakers, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), 2022, N'SIMF 2022', N'سيمف 2022',
        N'The inaugural edition — charting a course for regional maritime security.',
        N'النسخة الأولى — رسم مسار الأمن البحري الإقليمي.',
        N'Riyadh · Saudi Arabia', N'الرياض · المملكة العربية السعودية',
        N'November 2022 · 3 days', N'نوفمبر 2022 · 3 أيام', 800, 24, 30, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveEditions WHERE Year = 2023)
    INSERT INTO dbo.ArchiveEditions (Id, Year, TitleEn, TitleAr, SummaryEn, SummaryAr,
        LocationEn, LocationAr, DateLabelEn, DateLabelAr, Attendees, Sessions, Speakers, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), 2023, N'SIMF 2023', N'سيمف 2023',
        N'Securing tomorrow''s seas — resilience across the maritime domain.',
        N'تأمين بحار الغد — المرونة عبر القطاع البحري.',
        N'Riyadh · Saudi Arabia', N'الرياض · المملكة العربية السعودية',
        N'November 2023 · 3 days', N'نوفمبر 2023 · 3 أيام', 1000, 32, 35, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveEditions WHERE Year = 2024)
    INSERT INTO dbo.ArchiveEditions (Id, Year, TitleEn, TitleAr, SummaryEn, SummaryAr,
        LocationEn, LocationAr, DateLabelEn, DateLabelAr, Attendees, Sessions, Speakers, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), 2024, N'SIMF 2024', N'سيمف 2024',
        N'Resilient maritime supply chains for a connected world.',
        N'سلاسل إمداد بحرية مرنة لعالم مترابط.',
        N'Riyadh · Saudi Arabia', N'الرياض · المملكة العربية السعودية',
        N'November 2024 · 3 days', N'نوفمبر 2024 · 3 أيام', 1100, 38, 38, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ArchiveEditions WHERE Year = 2025)
    INSERT INTO dbo.ArchiveEditions (Id, Year, TitleEn, TitleAr, SummaryEn, SummaryAr,
        LocationEn, LocationAr, DateLabelEn, DateLabelAr, Attendees, Sessions, Speakers, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), 2025, N'SIMF 2025', N'سيمف 2025',
        N'The fourth edition — the future of seabed security and supply chains.',
        N'النسخة الرابعة — مستقبل أمن قاع البحار وسلاسل الإمداد.',
        N'Riyadh · Saudi Arabia', N'الرياض · المملكة العربية السعودية',
        N'November 2025 · 3 days', N'نوفمبر 2025 · 3 أيام', 1200, 40, 40, 1, @now, @sys);

/* ---------------------------------------------------------------------
   2) 2024 edition child lists (Figma 925-3079): session titles + past
   speakers. Only when the edition currently has NONE, so an admin edit is
   never overwritten. When the past-speaker list is seeded, the head
   counters are aligned to the frame (250 speakers, 30 events) once.
   --------------------------------------------------------------------- */
DECLARE @ed2024 uniqueidentifier = (SELECT TOP 1 Id FROM dbo.ArchiveEditions WHERE Year = 2024);

IF @ed2024 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveSessionTitles WHERE ArchiveEditionId = @ed2024)
BEGIN
    INSERT INTO dbo.ArchiveSessionTitles (Id, ArchiveEditionId, TitleEn, TitleAr, DisplayOrder)
    VALUES
        (NEWID(), @ed2024, N'Opening Session: The Future of Maritime Security', N'الجلسة الافتتاحية: مستقبل الأمن البحري', 0),
        (NEWID(), @ed2024, N'Protecting Corridors and Ports', N'حماية الممرات والموانئ', 1),
        (NEWID(), @ed2024, N'Modern Maritime Technologies', N'التقنيات البحرية الحديثة', 2);
END

IF @ed2024 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.ArchivePastSpeakers WHERE ArchiveEditionId = @ed2024)
BEGIN
    INSERT INTO dbo.ArchivePastSpeakers (Id, ArchiveEditionId, NameEn, NameAr, PhotoRelativePath, CountryId, DisplayOrder)
    VALUES
        (NEWID(), @ed2024, N'Mr. Ali',   N'أ. علي',  NULL, @saudi, 0),
        (NEWID(), @ed2024, N'Dr. Khalid', N'د. خالد', NULL, @saudi, 1),
        (NEWID(), @ed2024, N'Eng. Ahmed', N'م. أحمد', NULL, @saudi, 2),
        (NEWID(), @ed2024, N'Ms. Sara',  N'أ. سارة', NULL, @saudi, 3),
        (NEWID(), @ed2024, N'Eng. Fahd',  N'م. فهد',  NULL, @saudi, 4);

    -- Align the head-stat counters with Figma 925-3079, once, alongside the
    -- first child seed (a later admin edit is never overwritten).
    UPDATE dbo.ArchiveEditions SET Speakers = 250, Sessions = 30 WHERE Id = @ed2024;
END

COMMIT TRANSACTION;
