/* =====================================================================
   SIMF_App — demo seed for the 4 empty app screens
   (Booths / Exhibition · Delegations · FAQ · Venue map)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded by IF NOT EXISTS and
                     delegations are an idempotent UPDATE. Re-running adds
                     nothing new.
   Transactional   : the whole script is one transaction with
                     SET XACT_ABORT ON, so ANY error rolls the whole thing
                     back — no partial data.

   Prerequisites already present in production (verified via the public
   read API): the Main Hall (Halls.Code = 'MAIN') and the country lookup
   (Countries). This script only fills the four collections that the app
   currently returns empty.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now      datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys      uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)
DECLARE @hallId   uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Halls WHERE Code = N'MAIN' AND IsActive = 1);

/* ---------------------------------------------------------------------
   1) BOOTHS  (المعرض / Exhibition)  -> GET /app/booths
   Free-text exhibitor name is the designed fallback when a booth is not
   linked to a curated Exhibitor row (public projection uses it), so no
   Exhibitor rows are required. Placed in the Main Hall; MapX/MapY seed the
   "view on map" hint.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'A-01')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'A-01', N'Advanced Naval Technologies', N'التقنيات البحرية المتقدمة',
        N'Advanced Naval Technologies', N'التقنيات البحرية المتقدمة',
        N'Defense Systems', N'أنظمة الدفاع',
        N'Next-generation naval platforms, radar and combat-management systems for regional fleets.',
        N'منصات بحرية من الجيل القادم وأنظمة رادار وإدارة قتالية للأساطيل الإقليمية.',
        @hallId, 140, 460, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'A-02')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'A-02', N'Red Sea Marine Services', N'خدمات البحر الأحمر البحرية',
        N'Red Sea Marine Services', N'خدمات البحر الأحمر البحرية',
        N'Marine Services', N'الخدمات البحرية',
        N'Port operations, vessel maintenance and offshore support across the Red Sea corridor.',
        N'عمليات الموانئ وصيانة السفن والدعم البحري على امتداد ممر البحر الأحمر.',
        @hallId, 300, 460, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'B-01')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'B-01', N'Gulf Shipbuilding Co.', N'شركة الخليج لبناء السفن',
        N'Gulf Shipbuilding Co.', N'شركة الخليج لبناء السفن',
        N'Shipbuilding', N'بناء السفن',
        N'Design and construction of naval and commercial vessels, and localized MRO capacity.',
        N'تصميم وبناء السفن الحربية والتجارية وقدرات الإصلاح والصيانة المحلية.',
        @hallId, 140, 300, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'B-02')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'B-02', N'Maritime Cyber Defense', N'الدفاع السيبراني البحري',
        N'Maritime Cyber Defense', N'الدفاع السيبراني البحري',
        N'Cybersecurity', N'الأمن السيبراني',
        N'Protecting port infrastructure and shipboard OT networks from cyber threats.',
        N'حماية البنية التحتية للموانئ وشبكات التشغيل على متن السفن من التهديدات السيبرانية.',
        @hallId, 300, 300, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'C-01')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'C-01', N'Offshore Energy Solutions', N'حلول الطاقة البحرية',
        N'Offshore Energy Solutions', N'حلول الطاقة البحرية',
        N'Energy', N'الطاقة',
        N'Subsea pipelines, offshore platforms and marine renewable-energy systems.',
        N'خطوط الأنابيب البحرية والمنصات البحرية وأنظمة الطاقة المتجددة البحرية.',
        @hallId, 140, 140, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Booths WHERE Code = N'C-02')
    INSERT INTO dbo.Booths (Id, Code, Name, NameArabic, ExhibitorName, ExhibitorNameArabic,
        Sector, SectorArabic, Description, DescriptionArabic, HallId, MapX, MapY, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'C-02', N'Port Logistics Systems', N'أنظمة الخدمات اللوجستية للموانئ',
        N'Port Logistics Systems', N'أنظمة الخدمات اللوجستية للموانئ',
        N'Logistics', N'الخدمات اللوجستية',
        N'Smart terminal automation, cargo tracking and supply-chain integration.',
        N'أتمتة المحطات الذكية وتتبع البضائع وتكامل سلاسل الإمداد.',
        @hallId, 300, 140, @now, @sys, 1);

/* ---------------------------------------------------------------------
   2) DELEGATIONS  (الوفود)  -> GET /app/delegations
   A country appears when IsInvited = 1 AND IsActive = 1. This marks the
   visiting delegations and their arrival/departure window. The head of
   delegation and the member count populate automatically as real
   delegates register (they are resolved from UserProfiles at read time).
   Idempotent UPDATE — only touches rows whose Code exists.
   --------------------------------------------------------------------- */
UPDATE dbo.Countries
   SET IsInvited = 1,
       DelegationArrivalDate   = '2026-11-19',
       DelegationDepartureDate = '2026-11-23'
 WHERE Code IN (N'AE', N'BH', N'KW', N'OM', N'QA', N'EG', N'GB', N'US', N'FR', N'PK', N'IN', N'TR')
   AND IsActive = 1;

/* ---------------------------------------------------------------------
   3) FAQ  (الأسئلة الشائعة)  -> GET /app/faq
   Groups first, then their entries. Group id is resolved (existing or new)
   so entries always bind to the right group on a re-run.
   --------------------------------------------------------------------- */
DECLARE @gReg uniqueidentifier = (SELECT TOP 1 Id FROM dbo.FaqGroups WHERE Name = N'Registration');
IF @gReg IS NULL
BEGIN
    SET @gReg = NEWID();
    INSERT INTO dbo.FaqGroups (Id, Name, NameArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (@gReg, N'Registration', N'التسجيل', 0, @now, @sys, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gReg AND Question = N'How do I register for the forum?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gReg, N'How do I register for the forum?', N'كيف أسجل في الملتقى؟',
        N'Download the SIMF app, create an account, then complete your visitor profile. Your account is activated after review.',
        N'حمّل تطبيق سيمف، أنشئ حسابًا، ثم أكمل ملفك التعريفي كزائر. يُفعّل حسابك بعد المراجعة.',
        0, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gReg AND Question = N'Is there a registration fee?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gReg, N'Is there a registration fee?', N'هل توجد رسوم للتسجيل؟',
        N'Attendance is by invitation and is complimentary for approved participants.',
        N'الحضور بالدعوة ومجاني للمشاركين المعتمدين.',
        1, @now, @sys, 1);

DECLARE @gVenue uniqueidentifier = (SELECT TOP 1 Id FROM dbo.FaqGroups WHERE Name = N'Venue & Access');
IF @gVenue IS NULL
BEGIN
    SET @gVenue = NEWID();
    INSERT INTO dbo.FaqGroups (Id, Name, NameArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (@gVenue, N'Venue & Access', N'المكان والدخول', 1, @now, @sys, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gVenue AND Question = N'How do I use my entry badge?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gVenue, N'How do I use my entry badge?', N'كيف أستخدم بطاقة الدخول؟',
        N'Open the app, go to your entry badge, and present the QR code at the gate for scanning.',
        N'افتح التطبيق، انتقل إلى بطاقة الدخول، واعرض رمز الاستجابة السريعة عند البوابة ليتم مسحه.',
        0, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gVenue AND Question = N'Where is the forum held?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gVenue, N'Where is the forum held?', N'أين يُقام الملتقى؟',
        N'The forum is held at the main venue in the Kingdom of Saudi Arabia; the in-app venue map shows the halls and booths.',
        N'يُقام الملتقى في المقر الرئيسي بالمملكة العربية السعودية؛ وتعرض خريطة الموقع داخل التطبيق القاعات والأجنحة.',
        1, @now, @sys, 1);

DECLARE @gProg uniqueidentifier = (SELECT TOP 1 Id FROM dbo.FaqGroups WHERE Name = N'Sessions & Program');
IF @gProg IS NULL
BEGIN
    SET @gProg = NEWID();
    INSERT INTO dbo.FaqGroups (Id, Name, NameArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (@gProg, N'Sessions & Program', N'الجلسات والبرنامج', 2, @now, @sys, 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gProg AND Question = N'How do I book a session seat?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gProg, N'How do I book a session seat?', N'كيف أحجز مقعدًا في جلسة؟',
        N'Open the session from the programme and tap "Book seat". You can cancel from the same screen.',
        N'افتح الجلسة من البرنامج واضغط "حجز مقعد". يمكنك الإلغاء من الشاشة نفسها.',
        0, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.FaqEntries WHERE FaqGroupId = @gProg AND Question = N'Can I watch sessions live?')
    INSERT INTO dbo.FaqEntries (Id, FaqGroupId, Question, QuestionArabic, Answer, AnswerArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), @gProg, N'Can I watch sessions live?', N'هل يمكنني مشاهدة الجلسات مباشرة؟',
        N'Yes. Sessions with a live stream show a "Live" option; a session summary is published afterwards.',
        N'نعم. تعرض الجلسات التي تحتوي على بث مباشر خيار "مباشر"، ويُنشر ملخص الجلسة بعد انتهائها.',
        1, @now, @sys, 1);

/* ---------------------------------------------------------------------
   4) VENUE MAP NODES  (الخريطة)  -> GET /app/venue-map
   Kinds: 0=Hall, 1=Zone, 2=Booth, 3=PointOfInterest.
   Check constraint: HallId => Kind=0 ; BoothId => Kind=2 ; not both set.
   --------------------------------------------------------------------- */
-- Main hall node
IF @hallId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.VenueMapNodes WHERE Kind = 0 AND HallId = @hallId)
    INSERT INTO dbo.VenueMapNodes (Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Main Hall', N'القاعة الرئيسية', 0, 500, 220, @hallId, NULL, @now, @sys, 1);

-- Booth nodes (Kind = 2) referencing the booths seeded above
INSERT INTO dbo.VenueMapNodes (Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), b.Name, b.NameArabic, 2, b.MapX, b.MapY, NULL, b.Id, @now, @sys, 1
  FROM dbo.Booths b
 WHERE b.Code IN (N'A-01', N'A-02', N'B-01', N'B-02', N'C-01', N'C-02')
   AND b.IsActive = 1
   AND NOT EXISTS (SELECT 1 FROM dbo.VenueMapNodes n WHERE n.Kind = 2 AND n.BoothId = b.Id);

-- Free-standing points of interest / zone
IF NOT EXISTS (SELECT 1 FROM dbo.VenueMapNodes WHERE Kind = 3 AND Label = N'Registration Desk')
    INSERT INTO dbo.VenueMapNodes (Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Registration Desk', N'مكتب التسجيل', 3, 80, 80, NULL, NULL, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.VenueMapNodes WHERE Kind = 3 AND Label = N'Prayer Room')
    INSERT INTO dbo.VenueMapNodes (Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Prayer Room', N'مصلى', 3, 900, 80, NULL, NULL, @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.VenueMapNodes WHERE Kind = 1 AND Label = N'Exhibition Zone')
    INSERT INTO dbo.VenueMapNodes (Id, Label, LabelArabic, Kind, X, Y, HallId, BoothId, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Exhibition Zone', N'منطقة المعرض', 1, 220, 300, NULL, NULL, @now, @sys, 1);

COMMIT TRANSACTION;

/* Post-run summary (safe to read) */
SELECT 'Booths'          AS Collection, COUNT(*) AS Rows FROM dbo.Booths        WHERE IsActive = 1
UNION ALL SELECT 'Invited countries', COUNT(*) FROM dbo.Countries     WHERE IsInvited = 1 AND IsActive = 1
UNION ALL SELECT 'FaqGroups',         COUNT(*) FROM dbo.FaqGroups      WHERE IsActive = 1
UNION ALL SELECT 'FaqEntries',        COUNT(*) FROM dbo.FaqEntries     WHERE IsActive = 1
UNION ALL SELECT 'VenueMapNodes',     COUNT(*) FROM dbo.VenueMapNodes  WHERE IsActive = 1;
