/* =====================================================================
   SIMF_App — organisation profile seed
              (About / Vision / Mission / Themes + key-fact details + social links)
              ->  GET /app/organization

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — the about-items block runs ONCE (guarded by the
                     "About the Forum" marker) so a later admin edit is never
                     clobbered; the social-links block sets each URL only when
                     it is currently empty (COALESCE/NULLIF).
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder.EnsureOrganizationAboutItems
                     (D-586) + EnsureOrganizationSocialLinks; the content-seeding
                     lane moved from C# to this SQL file (D-718 / owner rule — see
                     README). The singleton OrganizationProfile row itself is
                     created by EF HasData (D-495) — this only fills its content.

   >>> The four about items are the REAL public deck text. The social handles
       are PLACEHOLDERS — replace with the official accounts before handover.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)
DECLARE @org uniqueidentifier = '00000000-0000-0000-0000-000000000003'; -- OrganizationProfile.SingletonId

/* ---------------------------------------------------------------------
   1) ABOUT ITEMS  — run ONCE, keyed on the "About the Forum" marker.
   Each item is upserted by Title so the D-495 HasData placeholders
   (Vision + Mission) are rewritten in place while About + Key Themes are
   inserted. Guarding on the About marker means a later admin edit (which
   keeps the About row) is never re-touched.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.OrganizationAboutItems WHERE Title = N'About the Forum')
BEGIN
    -- (DisplayOrder, Title, TitleArabic, Text, TextArabic)
    DECLARE @items TABLE (
        Ord   int,
        Ttl   nvarchar(256),
        TtlAr nvarchar(256),
        Txt   nvarchar(4000),
        TxtAr nvarchar(4000)
    );
    INSERT INTO @items (Ord, Ttl, TtlAr, Txt, TxtAr) VALUES
    (0, N'About the Forum', N'نبذة عن الملتقى',
     N'Recent regional and international developments have shown that maritime security is no longer limited to protecting shipping lanes — it is now directly tied to energy security and the global economy. On this basis, the Fourth Saudi International Maritime Forum is held under the theme "The Future of Seabed Security and Supply Chains in a Changing Global Environment", to address threats to seabed security and supply chains with the participation of international experts from leading universities, strategic-studies centres, and relevant organisations to exchange expertise and develop solutions.',
     N'أثبتت المتغيرات الإقليمية والدولية خلال الفترة الأخيرة أن أمن البحار لم يعد مقتصراً على حماية خطوط الملاحة فحسب، بل أصبح يرتبط بشكل مباشر بأمن الطاقة والاقتصاد العالمي. ومن هذا المنطلق جاء الملتقى البحري السعودي الدولي الرابع بعنوان (مستقبل أمن قاع البحار وسلاسل الإمداد في بيئة عالمية متغيرة)، لمناقشة مهددات أمن قاع البحار وسلاسل الإمداد بمشاركة خبراء دوليين من أشهر الجامعات ومراكز الدراسات الاستراتيجية والمنظمات ذات العلاقة لتبادل الخبرات وإيجاد الحلول.'),
    (1, N'Vision', N'الرؤية',
     N'To become a globally leading platform for exchanging knowledge and advancing maritime innovation and industry, and for strengthening international cooperation to safeguard maritime security and the sustainability of oceans and marine resources.',
     N'أن يصبح الملتقى منصة رائدة عالمياً لتبادل المعرفة وتطوير الابتكارات والصناعة في المجال البحري، وتعزيز التعاون الدولي من أجل الحفاظ على الأمن البحري واستدامة المحيطات والموارد البحرية.'),
    (2, N'Mission', N'الرسالة',
     N'To bring together military leaders, local and global industry pioneers, researchers, and experts from around the world to exchange ideas, strengthen partnerships, and discuss the challenges and opportunities in the maritime sector — reinforcing the pivotal regional role of the Royal Saudi Naval Forces in advancing maritime security, supporting innovation, and sustaining the marine environment through purposeful dialogue, scientific contributions, and training activities.',
     N'جمع القادة العسكريين ورواد الصناعة المحلية والعالمية والباحثين والخبراء من جميع أنحاء العالم لتبادل الأفكار وتعزيز الشراكات ومناقشة التحديات والفرص في القطاع البحري، لتعزيز دور القوات البحرية الملكية السعودية المحوري في المنطقة في تعزيز الأمن البحري ودعم الابتكار واستدامة البيئة البحرية من خلال حوارات هادفة ومشاركات علمية وفعاليات تدريبية.'),
    (3, N'Key Themes', N'المحاور الرئيسية',
     N'1) Shifts in the global strategic environment and their impact on the security of maritime supply chains.
2) Threats to energy supply chains and their effect on the global economy.
3) Seabed security and undersea communications infrastructure.
4) Cybersecurity of maritime transport: challenges and solutions.
5) The role of artificial intelligence and modern technologies in seabed and supply-chain security.',
     N'١) المتغيرات في البيئة الاستراتيجية العالمية وتأثيرها على أمن سلاسل الإمداد البحرية.
٢) التهديدات على سلاسل إمداد الطاقة وأثرها على الاقتصاد العالمي.
٣) أمن قاع البحار وبنية الاتصالات تحت البحر.
٤) الأمن السيبراني للنقل البحري: التحديات والحلول.
٥) دور الذكاء الاصطناعي والتقنيات الحديثة في أمن قاع البحار وسلاسل الإمداد.');

    -- Rewrite the matching D-495 placeholder (by Title) in place …
    UPDATE a
       SET a.TitleArabic = i.TtlAr,
           a.Text         = i.Txt,
           a.TextArabic   = i.TxtAr,
           a.DisplayOrder = i.Ord,
           a.IsActive     = 1
      FROM dbo.OrganizationAboutItems a
      JOIN @items i ON i.Ttl = a.Title;

    -- … or insert the item when there is no row with that Title yet.
    INSERT INTO dbo.OrganizationAboutItems
        (Id, OrganizationProfileId, Title, TitleArabic, Text, TextArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    SELECT NEWID(), @org, i.Ttl, i.TtlAr, i.Txt, i.TxtAr, i.Ord, 1, @now, @sys
      FROM @items i
     WHERE NOT EXISTS (SELECT 1 FROM dbo.OrganizationAboutItems a WHERE a.Title = i.Ttl);
END

/* ---------------------------------------------------------------------
   1b) DETAILS  — the ordered name/value facts (Organiser / Edition / Dates)
   shown beside the About text on the D-495 org page. Real, sourced from the
   same 2026 deck (Fourth Forum, Royal Saudi Naval Forces) and the Programme
   seed (23-25 Nov 2026). Each row is inserted only when a row with that Name
   does not already exist, so a later admin edit is never clobbered.
   The D-747 port of the org content seeded the About items but omitted these
   Details — this closes that gap.
   --------------------------------------------------------------------- */
DECLARE @details TABLE (Ord int, Nm nvarchar(256), NmAr nvarchar(256), Val nvarchar(1024), ValAr nvarchar(1024));
INSERT INTO @details (Ord, Nm, NmAr, Val, ValAr) VALUES
(0, N'Organiser', N'الجهة المنظمة', N'Royal Saudi Naval Forces', N'القوات البحرية الملكية السعودية'),
(1, N'Edition',   N'النسخة',        N'Fourth (2026)',            N'الرابعة (2026)'),
(2, N'Dates',     N'التواريخ',      N'23-25 November 2026',      N'23-25 نوفمبر 2026');

INSERT INTO dbo.OrganizationDetails
    (Id, OrganizationProfileId, Name, NameArabic, Value, ValueArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), @org, d.Nm, d.NmAr, d.Val, d.ValAr, d.Ord, 1, @now, @sys
  FROM @details d
 WHERE NOT EXISTS (
     SELECT 1 FROM dbo.OrganizationDetails od
      WHERE od.OrganizationProfileId = @org AND od.Name = d.Nm);

/* Backfill the Arabic value on an ALREADY-seeded row that predates the
   ValueArabic column (D-762). Only when it is currently NULL and the English
   Value still matches the seed, so a later admin edit is never clobbered. */
UPDATE od
   SET od.ValueArabic = d.ValAr
  FROM dbo.OrganizationDetails od
  JOIN @details d
    ON d.Nm = od.Name AND d.Val = od.Value
 WHERE od.OrganizationProfileId = @org
   AND od.ValueArabic IS NULL;

/* Correct an ALREADY-seeded Dates row (old 20-22 value) to the real programme
   dates (23-25 Nov 2026). Guarded to the EXACT old seed value, so a later admin
   edit is never clobbered; a fresh DB gets 23-25 from the INSERT above instead. */
UPDATE dbo.OrganizationDetails
   SET Value = N'23-25 November 2026'
 WHERE OrganizationProfileId = @org
   AND Name  = N'Dates'
   AND Value = N'20–22 November 2026';

/* ---------------------------------------------------------------------
   2) SOCIAL LINKS  — set each field only when currently empty, so an admin
   edit (or the D-495 migrated values) is never overwritten. PLACEHOLDER
   handles — replace with the official accounts before handover.
   --------------------------------------------------------------------- */
UPDATE dbo.OrganizationProfile
   SET XUrl           = COALESCE(NULLIF(XUrl, N''),           N'https://x.com/SIMF_RSNF'),
       InstagramUrl   = COALESCE(NULLIF(InstagramUrl, N''),   N'https://instagram.com/simf_rsnf'),
       LinkedInUrl    = COALESCE(NULLIF(LinkedInUrl, N''),    N'https://linkedin.com/company/simf-rsnf'),
       YouTubeUrl     = COALESCE(NULLIF(YouTubeUrl, N''),     N'https://youtube.com/@SIMF_RSNF'),
       TikTokUrl      = COALESCE(NULLIF(TikTokUrl, N''),      N'https://tiktok.com/@simf_rsnf'),
       ContactWebsite = COALESCE(NULLIF(ContactWebsite, N''), N'https://simf.gov.sa')
 WHERE Id = @org;

COMMIT TRANSACTION;
