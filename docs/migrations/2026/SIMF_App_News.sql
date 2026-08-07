/* =====================================================================
   SIMF_App — news seed  (Highlights item)  ->  GET /app/news

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — guarded by IF NOT EXISTS on the Title marker.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from DefaultContentSeeder (D-681); the
                     content-seeding lane moved from C# to this SQL file
                     (D-718 / owner rule — see README).

   >>> PLACEHOLDER content — one "Highlights" article so the app's news
       strip is not empty. Replace / extend with the client's real 2026
       news items. The hero image is uploaded/linked by an editor via the
       Control Panel (ImageRelativePath is left NULL here).
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

IF NOT EXISTS (SELECT 1 FROM dbo.News WHERE Title = N'Saudi International Maritime Forum')
    INSERT INTO dbo.News (Id, Title, TitleArabic, Excerpt, ExcerptArabic, Body, BodyArabic,
        Category, CategoryArabic, PublishedAt, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(),
        N'Saudi International Maritime Forum',
        N'الملتقى البحري السعودي الدولي',
        N'The Kingdom''s flagship maritime and naval forum.',
        N'الحدث البحري والدفاعي الأبرز في المملكة.',
        N'The Saudi International Maritime Forum brings together the maritime and defence community for three days of sessions, exhibitions and networking.',
        N'يجمع الملتقى البحري السعودي الدولي المجتمع البحري والدفاعي على مدى ثلاثة أيام من الجلسات والمعارض وفرص التواصل.',
        N'Highlights', N'أبرز الأحداث',
        @now, 0, 1, @now, @sys);

COMMIT TRANSACTION;
