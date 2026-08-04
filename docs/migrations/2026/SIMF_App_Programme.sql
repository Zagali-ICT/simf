/* =====================================================================
   SIMF_App - programme seed  (Hall . Themes . Programme days . Sessions)
                              -> GET /app/halls . /app/programme . /app/sessions

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES - every insert is guarded (hall by Code, themes by
                     Code, days by Date, sessions by Code, links by pair).
   Transactional   : the whole script is one transaction with
                     SET XACT_ABORT ON, so ANY error rolls the whole thing
                     back - no partial data.

   Run ORDER       : run this BEFORE SIMF_App_SeedGaps.sql - that file's
                     booths + venue-map nodes reference Halls.Code = 'MAIN',
                     which this file creates.

   Source          : the client's real 2026 programme deck
                     15-04-2024/محتوى الموقع الالكتروني.pptx
                     ("Fourth Saudi International Maritime Forum - website
                     content"). Theme: "The Future of Seabed Security and
                     Supply Chains in a Changing Global Environment". Three
                     plenary days in the main hall, five axes (محاور), three
                     keynotes, five panels, fifteen scientific papers.

   Dates           : 23-25 November 2026 (owner ruling - the public landing
                     hero already advertises these dates; the deck itself
                     labels the days One/Two/Three with no calendar date).

   Provenance      : replaces the earlier PLACEHOLDER seed (D-681 / D-718).
                     This file (a) soft-deletes the five demo sessions
                     (Codes S-D1-01..S-D3-01) and the three demo days
                     (20-22 Nov 2026) the placeholder created, (b) seeds the
                     five real axes as Themes, (c) seeds the three real days,
                     (d) seeds the full run-of-show (59 sessions) and links
                     each axis session to its Theme.

   Speakers        : SessionSpeakers is left EMPTY on purpose. The programme
                     deck names no speaker per session (papers are generic,
                     panels are "four experts + a moderator" with no names),
                     and the roster (SIMF_App_Speakers.sql) is "invited, not
                     yet confirmed" and not mapped to slots. An admin assigns
                     each speaker to a session in the Control Panel once the
                     roster is confirmed.

   Assumptions (adjustable by an admin in the CP):
     - Day 2, "Day 2 Summary & Recommendations" (D2-17): the deck shows
       15:00-14:35, which overlaps the axis-4 wrap-up and is almost certainly
       a typo for 14:45. Seeded here as 14:45-15:00.
     - Cultural rows (D1-28, D2-18, D3-13) have NO time in the deck; a nominal
       19:00-22:00 evening window is used (Description says "timing proposed").
     - English titles are translations of the Arabic-only deck.
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
   The single main hall (all three days are plenary). Unchanged from the
   placeholder - Capacity/Purpose/SeatSelectionMode mirror the app defaults.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Halls WHERE Code = N'MAIN')
    INSERT INTO dbo.Halls (Id, Code, Name, NameArabic, Capacity,
        Purpose, SeatSelectionMode, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'MAIN', N'Main Hall', N'القاعة الرئيسية', 500,
        0, 0, 1, @now, @sys);

DECLARE @hallId uniqueidentifier = (SELECT TOP 1 Id FROM dbo.Halls WHERE Code = N'MAIN');

/* ---------------------------------------------------------------------
   2) CLEANUP of the old PLACEHOLDER rows (owner: "guarded cleanup").
   Soft-delete (IsActive = 0) so it is FK-safe on an already-seeded DB and
   fully reversible. Matched on the EXACT demo identifiers only, so a real
   row an admin created is never touched; idempotent (re-running is a no-op).
   --------------------------------------------------------------------- */
UPDATE dbo.Sessions
   SET IsActive = 0, DeletedAt = @now, UpdatedAt = @now, UpdatedBy = @sys
 WHERE IsActive = 1
   AND Code IN (N'S-D1-01', N'S-D1-02', N'S-D2-01', N'S-D2-02', N'S-D3-01');

UPDATE dbo.ProgrammeDays
   SET IsActive = 0, DeletedAt = @now, UpdatedAt = @now, UpdatedBy = @sys
 WHERE IsActive = 1
   AND [Date] IN ('2026-11-20', '2026-11-21', '2026-11-22')
   AND Title IN (N'Day One', N'Day Two', N'Day Three');

/* ---------------------------------------------------------------------
   3) THEMES (the five محاور / axes)  ->  agenda pillar grouping.
   Guarded by Code. PageColor is a data value (like ProfileType.PageColor).
   --------------------------------------------------------------------- */
DECLARE @themes TABLE (
    Code    nvarchar(16),
    Nm      nvarchar(128),
    NmAr    nvarchar(128),
    Descr   nvarchar(1024),
    DescrAr nvarchar(1024),
    Color   nvarchar(32),
    Ord     int
);
INSERT INTO @themes (Code, Nm, NmAr, Descr, DescrAr, Color, Ord) VALUES
(N'AXIS-1', N'Global Strategic Environment & Supply-Chain Security',
    N'المتغيرات في البيئة الاستراتيجية العالمية وأمن سلاسل الإمداد',
    N'Axis 1 - Shifts in the global strategic environment and their impact on the security of maritime supply chains.',
    N'المحور الأول - المتغيرات في البيئة الاستراتيجية العالمية وتأثيرها على أمن سلاسل الإمداد البحرية.',
    N'#1B4965', 1),
(N'AXIS-2', N'Threats to Energy Supply Chains',
    N'التهديدات على سلاسل إمداد الطاقة وأثرها على الاقتصاد العالمي',
    N'Axis 2 - Threats to energy supply chains and their effect on the global economy.',
    N'المحور الثاني - التهديدات على سلاسل إمداد الطاقة وأثرها على الاقتصاد العالمي.',
    N'#C1440E', 2),
(N'AXIS-3', N'Seabed Security & Undersea Communications',
    N'أمن قاع البحار وبنية الاتصالات تحت البحر',
    N'Axis 3 - Seabed security and undersea communications infrastructure.',
    N'المحور الثالث - أمن قاع البحار وبنية الاتصالات تحت البحر.',
    N'#0B6E4F', 3),
(N'AXIS-4', N'Maritime Cybersecurity',
    N'الأمن السيبراني للنقل البحري: التحديات والحلول',
    N'Axis 4 - Cybersecurity of maritime transport: challenges and solutions.',
    N'المحور الرابع - الأمن السيبراني للنقل البحري: التحديات والحلول.',
    N'#5B2A86', 4),
(N'AXIS-5', N'AI & Modern Technologies in Maritime Security',
    N'دور الذكاء الاصطناعي والتقنيات الحديثة في أمن قاع البحار وسلاسل الإمداد',
    N'Axis 5 - The role of artificial intelligence and modern technologies in seabed and supply-chain security.',
    N'المحور الخامس - دور الذكاء الاصطناعي والتقنيات الحديثة في أمن قاع البحار وسلاسل الإمداد.',
    N'#2A6FB0', 5);

INSERT INTO dbo.Themes (Id, Code, Name, NameArabic, Description, DescriptionArabic,
        PageColor, DisplayOrder, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), t.Code, t.Nm, t.NmAr, t.Descr, t.DescrAr, t.Color, t.Ord, 1, @now, @sys
  FROM @themes t
 WHERE NOT EXISTS (SELECT 1 FROM dbo.Themes x WHERE x.Code = t.Code);

/* ---------------------------------------------------------------------
   4) PROGRAMME DAYS  ->  GET /app/programme
   Three days (23-25 Nov 2026). Guarded by active Date.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE [Date] = '2026-11-23' AND IsActive = 1)
    INSERT INTO dbo.ProgrammeDays (Id, [Date], Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-23', N'Day One', N'اليوم الأول', 0, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE [Date] = '2026-11-24' AND IsActive = 1)
    INSERT INTO dbo.ProgrammeDays (Id, [Date], Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-24', N'Day Two', N'اليوم الثاني', 1, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.ProgrammeDays WHERE [Date] = '2026-11-25' AND IsActive = 1)
    INSERT INTO dbo.ProgrammeDays (Id, [Date], Title, TitleArabic, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), '2026-11-25', N'Day Three', N'اليوم الثالث', 2, 1, @now, @sys);

/* ---------------------------------------------------------------------
   5) SESSIONS  ->  GET /app/sessions
   The full run-of-show: 59 rows across the three days. Times are event-local
   (UTC+3). Type: 1 = Session (talks/papers/panels/Q&A), 2 = Event (ceremony /
   breaks / cultural). Status = 0 (Scheduled). ThemeCode links axis sessions
   to their pillar (section 6 below). CK_Sessions_TimeWindow requires
   End > Start - every row satisfies it.
   --------------------------------------------------------------------- */
DECLARE @sessions TABLE (
    Code      nvarchar(16),
    TitleEn   nvarchar(256),
    TitleAr   nvarchar(256),
    DescrEn   nvarchar(2048),
    DescrAr   nvarchar(2048),
    TypeInt   int,
    StartDt   datetimeoffset,
    EndDt     datetimeoffset,
    ThemeCode nvarchar(16)
);
INSERT INTO @sessions (Code, TitleEn, TitleAr, DescrEn, DescrAr, TypeInt, StartDt, EndDt, ThemeCode) VALUES
-- ===== DAY 1  (2026-11-23)  Opening + Axis 1 + Axis 2 =====
(N'D1-01', N'Reception & Registration', N'الاستقبال والتسجيل', NULL, NULL, 2, '2026-11-23T07:00:00+03:00', '2026-11-23T08:15:00+03:00', NULL),
(N'D1-02', N'Arrival of the Chief of General Staff & Forum Opening', N'وصول معالي رئيس هيئة الأركان العامة وبدء فعاليات افتتاح الملتقى', NULL, NULL, 2, '2026-11-23T08:30:00+03:00', '2026-11-23T08:35:00+03:00', NULL),
(N'D1-03', N'Holy Quran Recitation', N'تلاوة آيات من القرآن الكريم', NULL, NULL, 2, '2026-11-23T08:35:00+03:00', '2026-11-23T08:38:00+03:00', NULL),
(N'D1-04', N'Patron Inaugurates the Forum', N'تدشين راعي الحفل لأعمال الملتقى', NULL, NULL, 2, '2026-11-23T08:38:00+03:00', '2026-11-23T08:40:00+03:00', NULL),
(N'D1-05', N'Forum Visual Presentation', N'العرض المرئي للملتقى البحري', NULL, NULL, 2, '2026-11-23T08:40:00+03:00', '2026-11-23T08:50:00+03:00', NULL),
(N'D1-06', N'Speech by the Chief of Naval Forces Staff', N'كلمة معالي رئيس أركان القوات البحرية', NULL, NULL, 2, '2026-11-23T08:50:00+03:00', '2026-11-23T09:00:00+03:00', NULL),
(N'D1-07', N'Commemorative Photo with the Patron', N'الصورة التذكارية مع راعي الحفل', NULL, NULL, 2, '2026-11-23T09:00:00+03:00', '2026-11-23T09:05:00+03:00', NULL),
(N'D1-08', N'Opening of the Accompanying Exhibition', N'افتتاح المعرض المصاحب', NULL, NULL, 2, '2026-11-23T09:05:00+03:00', '2026-11-23T09:06:00+03:00', NULL),
(N'D1-09', N'Tour of the Accompanying Exhibition', N'جولة على المعرض المصاحب', NULL, NULL, 2, '2026-11-23T09:06:00+03:00', '2026-11-23T09:20:00+03:00', NULL),
(N'D1-10', N'Departure of the Patron', N'مغادرة راعي الحفل', NULL, NULL, 2, '2026-11-23T09:20:00+03:00', '2026-11-23T09:30:00+03:00', NULL),
(N'D1-11', N'Keynote: Energy Supply-Chain Security', N'الجلسة الرئيسية: أمن سلاسل إمداد الطاقة', N'Day 1 highlights the security of energy supply chains and its role in the stability of global trade, with a representative of the Ministry of Energy taking part in the keynote session.', N'يُسلط اليوم الأول الضوء على أمن سلاسل إمداد الطاقة ودورها في استقرار التجارة العالمية، بمشاركة ممثل وزارة الطاقة في الجلسة الرئيسية.', 1, '2026-11-23T09:30:00+03:00', '2026-11-23T10:15:00+03:00', N'AXIS-1'),
(N'D1-12', N'Axis 1 Introduction', N'تقديم المحور الأول', N'Shifts in the global strategic environment and their impact on the security of maritime supply chains.', N'المتغيرات في البيئة الاستراتيجية العالمية وتأثيرها على أمن سلاسل الإمداد.', 1, '2026-11-23T10:15:00+03:00', '2026-11-23T10:20:00+03:00', N'AXIS-1'),
(N'D1-13', N'Axis 1 - Scientific Paper 1', N'المحور الأول - عرض الورقة العلمية الأولى', NULL, NULL, 1, '2026-11-23T10:20:00+03:00', '2026-11-23T10:35:00+03:00', N'AXIS-1'),
(N'D1-14', N'Axis 1 - Scientific Paper 2', N'المحور الأول - عرض الورقة العلمية الثانية', NULL, NULL, 1, '2026-11-23T10:35:00+03:00', '2026-11-23T10:50:00+03:00', N'AXIS-1'),
(N'D1-15', N'Axis 1 - Scientific Paper 3', N'المحور الأول - عرض الورقة العلمية الثالثة', NULL, NULL, 1, '2026-11-23T10:50:00+03:00', '2026-11-23T11:05:00+03:00', N'AXIS-1'),
(N'D1-16', N'Axis 1 - Q&A Session', N'المحور الأول - جلسة الأسئلة والأجوبة', NULL, NULL, 1, '2026-11-23T11:05:00+03:00', '2026-11-23T11:20:00+03:00', N'AXIS-1'),
(N'D1-17', N'Panel 1', N'حلقة نقاش (١)', N'Four subject-matter experts and a specialist session moderator.', N'أربعة خبراء في مجال المحور ومدير جلسة متخصص.', 1, '2026-11-23T11:25:00+03:00', '2026-11-23T12:25:00+03:00', N'AXIS-1'),
(N'D1-18', N'Axis 1 Wrap-up & Honoring', N'تلخيص واختتام المحور الأول وتكريم المتحدثين والمشاركين', NULL, NULL, 1, '2026-11-23T12:25:00+03:00', '2026-11-23T12:35:00+03:00', N'AXIS-1'),
(N'D1-19', N'Dhuhr Prayer & Lunch Break', N'استراحة صلاة الظهر والغداء', NULL, NULL, 2, '2026-11-23T12:35:00+03:00', '2026-11-23T13:35:00+03:00', NULL),
(N'D1-20', N'Axis 2 Introduction', N'تقديم المحور الثاني', N'Threats to energy supply chains and their impact on the global economy.', N'التهديدات على سلاسل إمداد الطاقة وأثرها على الاقتصاد العالمي.', 1, '2026-11-23T13:35:00+03:00', '2026-11-23T13:40:00+03:00', N'AXIS-2'),
(N'D1-21', N'Axis 2 - Scientific Paper 1', N'المحور الثاني - عرض الورقة العلمية الأولى', NULL, NULL, 1, '2026-11-23T13:55:00+03:00', '2026-11-23T14:10:00+03:00', N'AXIS-2'),
(N'D1-22', N'Axis 2 - Scientific Paper 2', N'المحور الثاني - عرض الورقة العلمية الثانية', NULL, NULL, 1, '2026-11-23T14:10:00+03:00', '2026-11-23T14:25:00+03:00', N'AXIS-2'),
(N'D1-23', N'Axis 2 - Scientific Paper 3', N'المحور الثاني - عرض الورقة العلمية الثالثة', NULL, NULL, 1, '2026-11-23T14:25:00+03:00', '2026-11-23T14:40:00+03:00', N'AXIS-2'),
(N'D1-24', N'Axis 2 - Q&A Session', N'المحور الثاني - جلسة الأسئلة والأجوبة', NULL, NULL, 1, '2026-11-23T14:40:00+03:00', '2026-11-23T14:55:00+03:00', N'AXIS-2'),
(N'D1-25', N'Panel 2', N'حلقة نقاش (٢)', N'Four subject-matter experts and a specialist session moderator.', N'أربعة خبراء في مجال المحور ومدير جلسة متخصص.', 1, '2026-11-23T15:00:00+03:00', '2026-11-23T16:00:00+03:00', N'AXIS-2'),
(N'D1-26', N'Axis 2 Wrap-up & Honoring', N'تلخيص واختتام المحور الثاني وتكريم المتحدثين', NULL, NULL, 1, '2026-11-23T16:00:00+03:00', '2026-11-23T16:10:00+03:00', N'AXIS-2'),
(N'D1-27', N'Day 1 Summary & Recommendations', N'عرض ملخص اليوم الأول والتوصيات', NULL, NULL, 1, '2026-11-23T16:10:00+03:00', '2026-11-23T16:20:00+03:00', NULL),
(N'D1-28', N'Day 1 Cultural Programme', N'البرنامج الثقافي - اليوم الأول', N'Proposed cultural programme (timing proposed): a tour of the Saudi National Museum and a tour of the Diriyah and Bujairi district.', N'البرنامج الثقافي المقترح (التوقيت مقترح): جولة في المتحف الوطني السعودي وجولة في منطقة الدرعية والبجيري.', 2, '2026-11-23T19:00:00+03:00', '2026-11-23T22:00:00+03:00', NULL),
-- ===== DAY 2  (2026-11-24)  Axis 3 + Axis 4 =====
(N'D2-01', N'Keynote: Undersea Communications Security', N'الجلسة الرئيسية: أمن بنية الاتصالات تحت البحر وأثره العالمي', N'Day 2 is dedicated to the security of the undersea communications infrastructure; it opens with a keynote session in which a representative of the Ministry of Communications and Information Technology takes part.', N'يخصص اليوم الثاني لمناقشة أمن البنية التحتية للاتصالات تحت البحر، وتُفتتح أعماله بجلسة رئيسية يشارك فيها ممثل وزارة الاتصالات وتقنية المعلومات.', 1, '2026-11-24T08:30:00+03:00', '2026-11-24T09:15:00+03:00', N'AXIS-3'),
(N'D2-02', N'Axis 3 Introduction', N'تقديم المحور الثالث', N'Seabed security and undersea communications infrastructure.', N'أمن قاع البحار وبنية الاتصالات تحت البحر.', 1, '2026-11-24T09:15:00+03:00', '2026-11-24T09:20:00+03:00', N'AXIS-3'),
(N'D2-03', N'Axis 3 - Scientific Paper 1', N'المحور الثالث - عرض الورقة العلمية الأولى', NULL, NULL, 1, '2026-11-24T09:20:00+03:00', '2026-11-24T09:35:00+03:00', N'AXIS-3'),
(N'D2-04', N'Axis 3 - Scientific Paper 2', N'المحور الثالث - عرض الورقة العلمية الثانية', NULL, NULL, 1, '2026-11-24T09:35:00+03:00', '2026-11-24T09:50:00+03:00', N'AXIS-3'),
(N'D2-05', N'Axis 3 - Scientific Paper 3', N'المحور الثالث - عرض الورقة العلمية الثالثة', NULL, NULL, 1, '2026-11-24T09:50:00+03:00', '2026-11-24T10:05:00+03:00', N'AXIS-3'),
(N'D2-06', N'Axis 3 - Q&A Session', N'المحور الثالث - جلسة الأسئلة والأجوبة', NULL, NULL, 1, '2026-11-24T10:05:00+03:00', '2026-11-24T10:20:00+03:00', N'AXIS-3'),
(N'D2-07', N'Panel 3', N'حلقة نقاش (٣)', N'Four subject-matter experts and a specialist session moderator.', N'أربعة خبراء في مجال المحور ومدير جلسة متخصص.', 1, '2026-11-24T10:20:00+03:00', '2026-11-24T11:20:00+03:00', N'AXIS-3'),
(N'D2-08', N'Axis 3 Wrap-up & Honoring', N'تلخيص واختتام المحور الثالث وتكريم المتحدثين والمشاركين', NULL, NULL, 1, '2026-11-24T11:20:00+03:00', '2026-11-24T11:30:00+03:00', N'AXIS-3'),
(N'D2-09', N'Dhuhr Prayer & Lunch Break', N'استراحة صلاة الظهر والغداء', NULL, NULL, 2, '2026-11-24T11:30:00+03:00', '2026-11-24T12:30:00+03:00', NULL),
(N'D2-10', N'Axis 4 Introduction', N'تقديم المحور الرابع', N'Cybersecurity of maritime transport: challenges and solutions.', N'الأمن السيبراني للنقل البحري: التحديات والحلول.', 1, '2026-11-24T12:30:00+03:00', '2026-11-24T12:35:00+03:00', N'AXIS-4'),
(N'D2-11', N'Axis 4 - Scientific Paper 1', N'المحور الرابع - عرض الورقة العلمية الأولى', NULL, NULL, 1, '2026-11-24T12:35:00+03:00', '2026-11-24T12:50:00+03:00', N'AXIS-4'),
(N'D2-12', N'Axis 4 - Scientific Paper 2', N'المحور الرابع - عرض الورقة العلمية الثانية', NULL, NULL, 1, '2026-11-24T12:50:00+03:00', '2026-11-24T13:05:00+03:00', N'AXIS-4'),
(N'D2-13', N'Axis 4 - Scientific Paper 3', N'المحور الرابع - عرض الورقة العلمية الثالثة', NULL, NULL, 1, '2026-11-24T13:05:00+03:00', '2026-11-24T13:20:00+03:00', N'AXIS-4'),
(N'D2-14', N'Axis 4 - Q&A Session', N'المحور الرابع - جلسة الأسئلة والأجوبة', NULL, NULL, 1, '2026-11-24T13:20:00+03:00', '2026-11-24T13:35:00+03:00', N'AXIS-4'),
(N'D2-15', N'Panel 4', N'حلقة نقاش (٤)', N'Four subject-matter experts and a specialist session moderator.', N'أربعة خبراء في مجال المحور ومدير جلسة متخصص.', 1, '2026-11-24T13:35:00+03:00', '2026-11-24T14:35:00+03:00', N'AXIS-4'),
(N'D2-16', N'Axis 4 Wrap-up & Honoring', N'تلخيص واختتام المحور الرابع وتكريم المتحدثين', NULL, NULL, 1, '2026-11-24T14:35:00+03:00', '2026-11-24T14:45:00+03:00', N'AXIS-4'),
-- D2-17: deck shows 15:00-14:35 (overlaps D2-16, likely typo for 14:45); seeded 14:45-15:00.
(N'D2-17', N'Day 2 Summary & Recommendations', N'عرض ملخص اليوم الثاني والتوصيات', NULL, NULL, 1, '2026-11-24T14:45:00+03:00', '2026-11-24T15:00:00+03:00', NULL),
(N'D2-18', N'Day 2 Cultural Evening', N'الأمسية الثقافية - اليوم الثاني', N'Proposed cultural evening (timing proposed). Proposed venues: King Fahd Cultural Center, Bait Issa (Irqah, Riyadh), Qasr Al-Murabba, Qasr Hittin.', N'الأمسية الثقافية المقترحة (التوقيت مقترح). المواقع المقترحة: مركز الملك فهد الثقافي، بيت عيسى (حي عرقة - الرياض)، قصر المربع، قصر حطين.', 2, '2026-11-24T19:00:00+03:00', '2026-11-24T22:00:00+03:00', NULL),
-- ===== DAY 3  (2026-11-25)  Axis 5 + Closing =====
(N'D3-01', N'Keynote Dialogue: AI in Maritime Supply-Chain Security', N'جلسة حوار المتحدث الرئيسي: دور الذكاء الاصطناعي في أمن سلاسل الإمداد البحرية', N'Day 3 focuses on employing artificial intelligence and emerging technologies to counter future threats targeting maritime infrastructure and supply chains, with the participation of a representative of the leading HUMAIN company or the Saudi Data and AI Authority (SDAIA).', N'يركز اليوم الثالث على توظيف الذكاء الاصطناعي والتقنيات الناشئة لمواجهة التهديدات المستقبلية التي تستهدف البنية التحتية البحرية وسلاسل الإمداد، بمشاركة ممثل شركة هيوماين الرائدة أو الهيئة السعودية للبيانات والذكاء الاصطناعي.', 1, '2026-11-25T08:30:00+03:00', '2026-11-25T09:15:00+03:00', N'AXIS-5'),
(N'D3-02', N'Axis 5 Introduction', N'تقديم المحور الخامس', N'The role of artificial intelligence and modern technologies in seabed and supply-chain security.', N'دور الذكاء الاصطناعي والتقنيات الحديثة في أمن قاع البحار وسلاسل الإمداد.', 1, '2026-11-25T09:15:00+03:00', '2026-11-25T09:20:00+03:00', N'AXIS-5'),
(N'D3-03', N'Axis 5 - Scientific Paper 1', N'المحور الخامس - عرض الورقة العلمية الأولى', NULL, NULL, 1, '2026-11-25T09:20:00+03:00', '2026-11-25T09:35:00+03:00', N'AXIS-5'),
(N'D3-04', N'Axis 5 - Scientific Paper 2', N'المحور الخامس - عرض الورقة العلمية الثانية', NULL, NULL, 1, '2026-11-25T09:35:00+03:00', '2026-11-25T09:50:00+03:00', N'AXIS-5'),
(N'D3-05', N'Axis 5 - Scientific Paper 3', N'المحور الخامس - عرض الورقة العلمية الثالثة', NULL, NULL, 1, '2026-11-25T09:50:00+03:00', '2026-11-25T10:05:00+03:00', N'AXIS-5'),
(N'D3-06', N'Axis 5 - Q&A Session', N'المحور الخامس - جلسة الأسئلة والأجوبة', NULL, NULL, 1, '2026-11-25T10:05:00+03:00', '2026-11-25T10:20:00+03:00', N'AXIS-5'),
(N'D3-07', N'Panel 5', N'حلقة نقاش (٥)', N'Four subject-matter experts and a specialist session moderator.', N'أربعة خبراء في مجال المحور ومدير جلسة متخصص.', 1, '2026-11-25T10:20:00+03:00', '2026-11-25T11:20:00+03:00', N'AXIS-5'),
(N'D3-08', N'Axis 5 Wrap-up & Honoring', N'تلخيص واختتام المحور الخامس وتكريم المتحدثين والمشاركين', NULL, NULL, 1, '2026-11-25T11:20:00+03:00', '2026-11-25T11:30:00+03:00', N'AXIS-5'),
(N'D3-09', N'Day 3 Summary & Final Recommendations', N'عرض ملخص اليوم الثالث والتوصيات النهائية', NULL, NULL, 1, '2026-11-25T11:30:00+03:00', '2026-11-25T11:40:00+03:00', NULL),
(N'D3-10', N'Honoring Sponsors & Participants', N'تكريم الرعاة والمشاركين', NULL, NULL, 2, '2026-11-25T11:40:00+03:00', '2026-11-25T12:00:00+03:00', NULL),
(N'D3-11', N'Forum Closing', N'اختتام فعاليات الملتقى', NULL, NULL, 2, '2026-11-25T12:00:00+03:00', '2026-11-25T12:30:00+03:00', NULL),
(N'D3-12', N'Dhuhr Prayer & Lunch Break', N'استراحة صلاة الظهر والغداء', NULL, NULL, 2, '2026-11-25T12:30:00+03:00', '2026-11-25T13:30:00+03:00', NULL),
(N'D3-13', N'Day 3 Cultural Programme', N'البرنامج الثقافي - اليوم الثالث', N'Proposed cultural programme (timing proposed): a visit to the cities of Makkah and Madinah.', N'البرنامج الثقافي المقترح (التوقيت مقترح): زيارة مدينتي مكة المكرمة والمدينة المنورة.', 2, '2026-11-25T19:00:00+03:00', '2026-11-25T22:00:00+03:00', NULL);

INSERT INTO dbo.Sessions (Id, Code, Title, TitleArabic, Description, DescriptionArabic,
        HallId, [Type], [Start], [End], Status, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), s.Code, s.TitleEn, s.TitleAr, s.DescrEn, s.DescrAr,
        @hallId, s.TypeInt, s.StartDt, s.EndDt, 0, 1, @now, @sys
  FROM @sessions s
 WHERE NOT EXISTS (SELECT 1 FROM dbo.Sessions x WHERE x.Code = s.Code);

/* ---------------------------------------------------------------------
   6) SESSION <-> THEME links  ->  agenda pillar grouping.
   Links every axis session (ThemeCode set above) to its Theme by Code.
   Guarded by the (SessionId, ThemeId) pair, so re-running adds nothing.
   --------------------------------------------------------------------- */
INSERT INTO dbo.SessionThemes (SessionId, ThemeId)
SELECT se.Id, th.Id
  FROM @sessions s
  JOIN dbo.Sessions se ON se.Code = s.Code
  JOIN dbo.Themes   th ON th.Code = s.ThemeCode
 WHERE s.ThemeCode IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.SessionThemes st
                    WHERE st.SessionId = se.Id AND st.ThemeId = th.Id);

COMMIT TRANSACTION;
