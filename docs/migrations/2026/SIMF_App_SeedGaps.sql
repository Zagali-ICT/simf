


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

   Prerequisites: the Main Hall (Halls.Code = 'MAIN') and the country lookup
   (Countries). Run SIMF_App_Programme.sql BEFORE this file — it creates the
   'MAIN' hall that the booths + venue-map nodes below reference (D-747). The
   country lookup is seeded by EF (CountryConfiguration.HasData). This script
   only fills the four collections that the app currently returns empty.
   ===================================================================== */

-- Required for INSERT/UPDATE on tables that carry filtered indexes
-- (UserProfiles, ProgrammeDays, …). SSMS sets these ON by default; sqlcmd
-- and some tools do not, so set them explicitly here.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
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
   1b) EXHIBITORS  -> country flag on each booth
   Each booth is linked to a curated Exhibitor whose inline CountryId
   drives the booth's country flag. D-766 removed the shared Contact
   directory and inlined the identity-card fields (country / city /
   website) directly onto the Exhibitor, so the exhibitor now carries
   the country itself - no Contact row.
   Idempotent: Exhibitors are guarded on Name, and the booth link is
   only written when Booths.ExhibitorId IS NULL.
   --------------------------------------------------------------------- */
DECLARE @ex TABLE (
    Code      nvarchar(16)  NOT NULL,
    ExName    nvarchar(256) NOT NULL,
    ExNameAr  nvarchar(256) NOT NULL,
    CountryId int           NOT NULL,
    City      nvarchar(128) NOT NULL,
    CityAr    nvarchar(128) NOT NULL,
    Website   nvarchar(512) NOT NULL,
    Tier      int           NOT NULL
);

INSERT INTO @ex (Code, ExName, ExNameAr, CountryId, City, CityAr, Website, Tier)
VALUES
    (N'A-01', N'Advanced Naval Technologies', N'التقنيات البحرية المتقدمة',
        682, N'Riyadh', N'الرياض',
        N'https://advanced-naval.example.com', 10),
    (N'A-02', N'Red Sea Marine Services', N'خدمات البحر الأحمر البحرية',
        784, N'Dubai', N'دبي',
        N'https://redsea-marine.example.com', 20),
    (N'B-01', N'Gulf Shipbuilding Co.', N'شركة الخليج لبناء السفن',
        826, N'Portsmouth', N'بورتسموث',
        N'https://gulf-shipbuilding.example.com', 10),
    (N'B-02', N'Maritime Cyber Defense', N'الدفاع السيبراني البحري',
        840, N'Norfolk', N'نورفولك',
        N'https://maritime-cyber.example.com', 20),
    (N'C-01', N'Offshore Energy Solutions', N'حلول الطاقة البحرية',
        250, N'Toulon', N'طولون',
        N'https://offshore-energy.example.com', 30),
    (N'C-02', N'Port Logistics Systems', N'أنظمة الخدمات اللوجستية للموانئ',
        356, N'Mumbai', N'مومباي',
        N'https://port-logistics.example.com', 30);

-- One Exhibitor per booth, with its country / city / website inline
-- (D-766 - the Contact directory was removed). Guard on Name.
INSERT INTO dbo.Exhibitors (Id, Name, NameArabic, CountryId, City, CityArabic,
    Website, Tier, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), e.ExName, e.ExNameAr, e.CountryId, e.City, e.CityAr,
    e.Website, e.Tier, 1, @now, @sys
  FROM @ex e
 WHERE NOT EXISTS (SELECT 1 FROM dbo.Exhibitors x WHERE x.Name = e.ExName);

-- Link each booth to its exhibitor (only when not already linked).
UPDATE b
   SET b.ExhibitorId = x.Id
  FROM dbo.Booths b
  JOIN @ex e            ON e.Code = b.Code
  JOIN dbo.Exhibitors x ON x.Name = e.ExName
 WHERE b.ExhibitorId IS NULL;

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
   2b) DELEGATION HEADS & MEMBERS  -> heads + member counts on الوفود
   For each invited country, seed one head UserProfile (JobTitle = the
   naval role, which marks it as the head) plus two member profiles, then
   point the country's HeadOfDelegationUserProfileId at that head. The
   country link is a logical reference (UserProfiles.NationalityId is a
   bare int FK to Countries.Id — no cross-DB constraint). UserId is a
   fresh NEWID() (unique); no Identity row is needed for the projection.
   Idempotent: heads are guarded per country (a head = IsDelegate = 1 AND
   JobTitle IS NOT NULL), members per country by Name, the pointer only
   when it is still NULL.
   --------------------------------------------------------------------- */
DECLARE @heads TABLE (
    CountryId int           NOT NULL,
    Nm        nvarchar(50)  NOT NULL,
    NmAr      nvarchar(50)  NOT NULL,
    Title     nvarchar(100) NOT NULL,
    TitleAr   nvarchar(100) NOT NULL,
    Pob       nvarchar(128) NOT NULL
);

INSERT INTO @heads (CountryId, Nm, NmAr, Title, TitleAr, Pob)
VALUES
    (784, N'Rashid Al Marri',   N'راشد المري',
        N'Commander, UAE Naval Forces',
        N'قائد القوات البحرية الإماراتية',        N'Abu Dhabi'),
    (48,  N'Khalid Al Khalifa', N'خالد آل خليفة',
        N'Commander, Bahrain Royal Navy',
        N'قائد القوة البحرية الملكية البحرينية',   N'Manama'),
    (414, N'Fahad Al Sabah',    N'فهد الصباح',
        N'Commander, Kuwait Naval Force',
        N'قائد القوة البحرية الكويتية',           N'Kuwait City'),
    (512, N'Sultan Al Balushi', N'سلطان البلوشي',
        N'Commander, Royal Navy of Oman',
        N'قائد البحرية السلطانية العُمانية',       N'Muscat'),
    (634, N'Nasser Al Thani',   N'ناصر آل ثاني',
        N'Commander, Qatar Emiri Naval Forces',
        N'قائد القوات البحرية القطرية',           N'Doha'),
    (818, N'Tarek Hassan',      N'طارق حسن',
        N'Commander, Egyptian Navy',
        N'قائد القوات البحرية المصرية',           N'Alexandria'),
    (826, N'James Whitfield',   N'جيمس ويتفيلد',
        N'First Sea Lord, Royal Navy',
        N'قائد البحرية الملكية البريطانية',        N'Portsmouth'),
    (840, N'Michael Carter',    N'مايكل كارتر',
        N'Chief of Naval Operations',
        N'قائد العمليات البحرية الأمريكية',        N'Norfolk'),
    (250, N'Henri Laurent',     N'هنري لوران',
        N'Chief of Staff, French Navy',
        N'رئيس أركان البحرية الفرنسية',           N'Toulon'),
    (586, N'Imran Qureshi',     N'عمران قريشي',
        N'Chief of Naval Staff, Pakistan',
        N'رئيس أركان البحرية الباكستانية',         N'Karachi'),
    (356, N'Arjun Nair',        N'أرجون نائير',
        N'Chief of Naval Staff, India',
        N'رئيس أركان البحرية الهندية',            N'Mumbai'),
    (792, N'Emre Yilmaz',       N'إمره يلماز',
        N'Commander, Turkish Naval Forces',
        N'قائد القوات البحرية التركية',           N'Istanbul');

DECLARE @members TABLE (
    Nm   nvarchar(50) NOT NULL,
    NmAr nvarchar(50) NOT NULL
);

INSERT INTO @members (Nm, NmAr)
VALUES
    (N'Delegation Officer',  N'ضابط الوفد'),
    (N'Delegation Attache',  N'ملحق الوفد');

-- Head of each delegation (JobTitle set => marks the row as the head).
-- AllowsDelegationMeeting / AllowsSpeakerMeeting are listed EXPLICITLY. They are
-- NOT NULL and, until the migrations were consolidated on 2026-07-31, carried a
-- DEFAULT of 0 left behind by AddUserProfileMeetingFlags' one-time backfill. The
-- consolidated InitialCreate builds the table from the model, which declares no
-- default, so omitting them here failed the whole seed with "Cannot insert the
-- value NULL into column 'AllowsDelegationMeeting'" and took the API down at boot
-- on any fresh database. 0 reproduces exactly what the old default produced.
INSERT INTO dbo.UserProfiles (Id, Name, NameArabic, JobTitle, NationalityId,
    PlaceOfBirth, Gender, IsDelegate, IsSaudi, IsActive, UserId, BadgeBatchId,
    EditionYear, AdmissionState, AllowsDelegationMeeting, AllowsSpeakerMeeting,
    CreatedAt, CreatedBy)
SELECT NEWID(), h.Nm, h.NmAr, h.Title, h.CountryId, h.Pob,
    0, 1, 0, 1, NULL, '0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0',
    (SELECT TOP 1 Year FROM dbo.EventEdition), N'Approved', 0, 0,
    @now, @sys
  FROM @heads h
 WHERE NOT EXISTS (
       SELECT 1 FROM dbo.UserProfiles p
        WHERE p.NationalityId = h.CountryId
          AND p.IsDelegate = 1
          AND p.JobTitle IS NOT NULL);

-- Two members per delegation (JobTitle NULL). Guarded per country by Name.
INSERT INTO dbo.UserProfiles (Id, Name, NameArabic, JobTitle, NationalityId,
    PlaceOfBirth, Gender, IsDelegate, IsSaudi, IsActive, UserId, BadgeBatchId,
    EditionYear, AdmissionState, AllowsDelegationMeeting, AllowsSpeakerMeeting,
    CreatedAt, CreatedBy)
SELECT NEWID(), m.Nm, m.NmAr, NULL, h.CountryId, N'-',
    0, 1, 0, 1, NULL, '0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0',
    (SELECT TOP 1 Year FROM dbo.EventEdition), N'Approved', 0, 0,
    @now, @sys
  FROM @heads h
 CROSS JOIN @members m
 WHERE NOT EXISTS (
       SELECT 1 FROM dbo.UserProfiles p
        WHERE p.NationalityId = h.CountryId
          AND p.Name = m.Nm);

-- Point each invited country at its head (only when not already set).
UPDATE c
   SET c.HeadOfDelegationUserProfileId = (
       SELECT TOP 1 p.Id
         FROM dbo.UserProfiles p
        WHERE p.NationalityId = c.Id
          AND p.IsDelegate = 1
          AND p.JobTitle IS NOT NULL
        ORDER BY p.CreatedAt)
  FROM dbo.Countries c
 WHERE c.IsInvited = 1
   AND c.HeadOfDelegationUserProfileId IS NULL;

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
UNION ALL SELECT 'VenueMapNodes',     COUNT(*) FROM dbo.VenueMapNodes  WHERE IsActive = 1
UNION ALL SELECT 'Exhibitors',        COUNT(*) FROM dbo.Exhibitors     WHERE IsActive = 1
UNION ALL SELECT 'Delegate profiles', COUNT(*) FROM dbo.UserProfiles   WHERE IsDelegate = 1 AND IsActive = 1;
