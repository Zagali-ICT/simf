/* =====================================================================
   SIMF_App — sponsors seed  ->  GET /app/sponsors

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded by IF NOT EXISTS (Name).
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder.EnsureDemoPartners
                     (D-348); the content-seeding lane moved from C# to this
                     SQL file (D-718 / owner rule — see README).

   Tiers (SponsorTier): Platinum = 10, Gold = 20, Silver = 30. The app renders
   three bands and localizes the tier HEADERS itself (Platinum -> strategic,
   Gold -> premium, Silver -> the gold-tile grid), so only Tier is stored here.
   No logo is seeded, by design — the app renders the name-initials fallback
   (Figma 922:2824); real logos are uploaded later via the Control Panel
   (StoredFile), the same way speaker photos are. The insert therefore names no
   logo column at all: the sponsor's pointer is now a typed LogoFileId keyed to
   dbo.StoredFiles, so leaving it out is the same as leaving it NULL and does
   not tie this seed to the column's spelling.

   >>> PLACEHOLDER content — the four named bodies are real SIMF partners but
       the six "Gold Sponsor N" rows are demo fillers. Replace / extend with
       the client's real 2026 sponsor list.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

/* -- Platinum (strategic) ------------------------------------------------ */
IF NOT EXISTS (SELECT 1 FROM dbo.Sponsors WHERE Name = N'Saudi Arabian Military Industries')
    INSERT INTO dbo.Sponsors (Id, Name, NameArabic, Tier, DisplayOrder, Tagline, TaglineArabic, About, AboutArabic, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Saudi Arabian Military Industries', N'الشركة السعودية للصناعات العسكرية', 10, 10,
        N'Strategic Sponsor · Defence-transformation partner of the Saudi International Maritime Forum',
        N'الراعي الاستراتيجي · شريك التحول الدفاعي للملتقى البحري السعودي الدولي',
        N'As the forum''s strategic sponsor, SAMI is the Kingdom''s national military-industries champion and the defence-transformation partner of the Saudi International Maritime Forum.',
        N'بصفتها الراعي الاستراتيجي للملتقى، تُعدّ الشركة السعودية للصناعات العسكرية البطل الوطني للصناعات العسكرية في المملكة وشريك التحول الدفاعي للملتقى البحري السعودي الدولي.',
        1, @now, @sys);

/* -- Gold (premium band) ------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Sponsors WHERE Name = N'General Authority for Military Industries')
    INSERT INTO dbo.Sponsors (Id, Name, NameArabic, Tier, DisplayOrder, Tagline, TaglineArabic, About, AboutArabic, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'General Authority for Military Industries', N'الهيئة العامة للصناعات العسكرية', 20, 10,
        N'Developing and enhancing the Kingdom''s military-industry capabilities',
        N'تطوير وتعزيز قدرات الصناعة العسكرية في المملكة',
        N'The General Authority for Military Industries develops and enhances the Kingdom''s military-industry capabilities, regulating and enabling the national defence sector.',
        N'تعمل الهيئة العامة للصناعات العسكرية على تطوير وتعزيز قدرات الصناعة العسكرية في المملكة وتنظيم وتمكين القطاع الدفاعي الوطني.',
        1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Sponsors WHERE Name = N'Royal Saudi Naval Forces')
    INSERT INTO dbo.Sponsors (Id, Name, NameArabic, Tier, DisplayOrder, Tagline, TaglineArabic, About, AboutArabic, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Royal Saudi Naval Forces', N'القوات البحرية الملكية السعودية', 20, 20,
        N'The naval arm of the armed forces and guardian of the coasts and corridors',
        N'الذراع البحري للقوات المسلحة وحامي السواحل والممرات',
        N'The Royal Saudi Naval Forces are the naval arm of the armed forces and the guardian of the Kingdom''s coasts and maritime corridors.',
        N'القوات البحرية الملكية السعودية هي الذراع البحري للقوات المسلحة وحامي سواحل المملكة وممراتها البحرية.',
        1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Sponsors WHERE Name = N'General Authority for Defense Development')
    INSERT INTO dbo.Sponsors (Id, Name, NameArabic, Tier, DisplayOrder, Tagline, TaglineArabic, About, AboutArabic, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'General Authority for Defense Development', N'الهيئة العامة للتطوير الدفاعي', 20, 30,
        N'Empowering the defence ecosystem and accelerating innovation in the sector',
        N'تمكين المنظومة الدفاعية وتسريع الابتكار في القطاع',
        N'The General Authority for Defense Development empowers the national defence ecosystem and accelerates innovation across the sector.',
        N'تعمل الهيئة العامة للتطوير الدفاعي على تمكين المنظومة الدفاعية الوطنية وتسريع الابتكار في القطاع.',
        1, @now, @sys);

/* -- Silver band — six demo "gold" fillers (Figma "رعاة ذهبيون" grid) ---- */
/* PLACEHOLDER: replace with the client's real gold-tier sponsor list. */
DECLARE @i   int = 1;
DECLARE @nm   nvarchar(256);
DECLARE @nmAr nvarchar(256);
WHILE @i <= 6
BEGIN
    SET @nm   = CONCAT(N'Gold Sponsor ', @i);
    SET @nmAr = CONCAT(N'راعي ذهبي ', @i);
    IF NOT EXISTS (SELECT 1 FROM dbo.Sponsors WHERE Name = @nm)
        INSERT INTO dbo.Sponsors (Id, Name, NameArabic, Tier, DisplayOrder, Tagline, TaglineArabic, About, AboutArabic, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), @nm, @nmAr, 30, @i * 10,
            N'Gold Sponsor of the Saudi International Maritime Forum',
            N'راعٍ ذهبي للملتقى البحري السعودي الدولي',
            N'A gold sponsor supporting the Saudi International Maritime Forum.',
            N'راعٍ ذهبي يدعم الملتقى البحري السعودي الدولي.',
            1, @now, @sys);
    SET @i = @i + 1;
END

COMMIT TRANSACTION;
