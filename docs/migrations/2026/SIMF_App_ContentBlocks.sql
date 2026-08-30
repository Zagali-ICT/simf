/* =====================================================================
   SIMF_App — CMS CONTENT BLOCKS seed  (54 bilingual rows)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded on the block Key.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder —
                     EnsureCybersecurityPolicyContentAsync (13 rows),
                     EnsureLandingHeroContentAsync (7),
                     EnsureLandingSectionsContentAsync (32) and
                     EnsureCoreAppContentAsync (2). The owner rule is that
                     IdentitySeeder keeps ONLY the identity bootstrap; every
                     other seed lives in this SQL lane, in one location, behind
                     one runner (Run_All_App_Seeds.sql).

   Who reads these
   ---------------
   • cyber.*   — the Flutter "سياسات وضوابط الأمن السيبراني" screen, via
                 GET /api/v1/content/cyber.*
   • hero.*    — the public marketing landing, via the Website's /content/site
                 proxy. Keys mirror SIMF.Common.LandingHeroContentKeys.
   • about.* / stats.* / pillars.* / goals.*
               — the landing's editorial sections below the hero. Keys mirror
                 SIMF.Common.LandingSectionContentKeys.
   • terms / about
               — the app's Terms and About screens. ONE TERM PER LINE: the app
                 splits on the newline and renders each line as one gold-bullet
                 card, so the separators below are NCHAR(10) concatenation and
                 NOT literal line breaks in this file (a literal break on
                 Windows would embed CR LF and put a stray carriage return at
                 the end of every card).

   Every key is admin-editable afterwards from the Control Panel's Content
   Blocks page; this seed only supplies the first version.

   NOTE on hero.metadate: the C# seeder derived this label from
   OrganizationProfile.DefaultEventStart / DefaultEventEnd through
   EventDateRange.Format. Those defaults are 2026-11-23 .. 2026-11-25, which is
   the literal below. If the edition dates change, edit this row (or the block
   itself from the CP) — it no longer tracks the constants automatically.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)
DECLARE @lf  nchar(1)         = NCHAR(10);                              -- the app's line separator; never a literal break

/* ---------------------------------------------------------------------
   The app's Terms and About copy, one item per line.
   --------------------------------------------------------------------- */
DECLARE @termsEn nvarchar(max) =
    N'These terms and conditions govern the use of the Saudi International Maritime Forum app and attendance at its events; by using the app you agree to them.' + @lf +
    N'Registration data must be accurate and match your official identity document; the forum administration may reject or cancel any incomplete or incorrect registration.' + @lf +
    N'Entry to the forum venue is by the personal QR code issued in the app after registration approval; it must not be shared with others.' + @lf +
    N'Bringing unlicensed photography or audio-recording equipment into the forum venue is prohibited.' + @lf +
    N'Visitors must follow all security and organisational instructions issued by the forum administration and security personnel across all facilities.' + @lf +
    N'Hazardous or legally prohibited materials are not allowed into the venue; bags and belongings are subject to security inspection.' + @lf +
    N'The organiser may photograph and film the events; by attending you consent to the use of such material for documentation and media purposes.' + @lf +
    N'Personal data is processed in accordance with the applicable laws of the Kingdom of Saudi Arabia and solely for the purposes of organising the forum.' + @lf +
    N'The forum administration may amend these terms, the event programme, or schedules when necessary; updates are announced through the app.';

DECLARE @termsAr nvarchar(max) =
    N'تسري هذه الشروط والأحكام على استخدام تطبيق الملتقى الدولي البحري وعلى حضور فعالياته، وباستخدامك للتطبيق فإنك توافق عليها.' + @lf +
    N'يجب أن تكون بيانات التسجيل صحيحة ومطابقة للهوية الرسمية، ويحق لإدارة الملتقى رفض أو إلغاء أي تسجيل غير مكتمل أو غير صحيح.' + @lf +
    N'الدخول إلى مقر الملتقى يتم بواسطة رمز الاستجابة السريعة (QR) الشخصي الصادر عبر التطبيق بعد اعتماد التسجيل، ولا يجوز مشاركته مع الغير.' + @lf +
    N'يُمنع إدخال أي أجهزة تصوير أو تسجيل صوتي غير مرخصة إلى مقر الملتقى.' + @lf +
    N'يلتزم الزائر بالتعليمات الأمنية والتنظيمية الصادرة عن إدارة الملتقى وأفراد الأمن في جميع المرافق.' + @lf +
    N'يُمنع إدخال المواد الخطرة أو الممنوعة نظاماً إلى مقر الملتقى، وتخضع الحقائب والمقتنيات للتفتيش الأمني.' + @lf +
    N'قد تقوم الجهة المنظمة بالتصوير الفوتوغرافي والمرئي للفعاليات، وبحضورك فإنك توافق على استخدام هذه المواد لأغراض التوثيق والإعلام.' + @lf +
    N'تُعالج بياناتك الشخصية وفق الأنظمة المعمول بها في المملكة العربية السعودية ولأغراض تنظيم الملتقى فقط.' + @lf +
    N'يحق لإدارة الملتقى تعديل هذه الشروط أو برنامج الفعاليات أو المواعيد عند الاقتضاء، ويتم الإشعار بأي تحديث عبر التطبيق.';

DECLARE @aboutEn nvarchar(max) =
    N'The Saudi International Maritime Forum is hosted by the Royal Saudi Naval Forces, bringing together decision-makers, experts, and leading companies of the maritime and defence sector from around the world.' + @lf +
    N'The forum aims to strengthen international cooperation, exchange expertise, and showcase the latest maritime technologies, supporting the goals of Saudi Vision 2030 in localising the defence and maritime industries.' + @lf +
    N'The programme includes panel sessions, workshops, an accompanying exhibition, and professional networking opportunities for participants and visitors.';

DECLARE @aboutAr nvarchar(max) =
    N'الملتقى الدولي البحري حدث تستضيفه القوات البحرية الملكية السعودية، يجمع صنّاع القرار والخبراء والشركات الرائدة في القطاع البحري والدفاعي من مختلف دول العالم.' + @lf +
    N'يهدف الملتقى إلى تعزيز التعاون الدولي وتبادل الخبرات واستعراض أحدث التقنيات البحرية، بما يدعم مستهدفات رؤية المملكة 2030 في توطين الصناعات الدفاعية والبحرية.' + @lf +
    N'يتضمن برنامج الملتقى جلسات حوارية وورش عمل ومعرضاً مصاحباً وفرصاً للتواصل المهني بين المشاركين والزوار.';

/* ---------------------------------------------------------------------
   The 54 blocks. Staged in a table variable, then inserted in ONE
   key-guarded statement — the same shape the exhibitor rows in
   SIMF_App_SeedGaps.sql use.
   --------------------------------------------------------------------- */
DECLARE @blocks TABLE (
    BlockKey      nvarchar(128)  NOT NULL PRIMARY KEY,
    Content       nvarchar(max)  NOT NULL,
    ContentArabic nvarchar(max)  NOT NULL);

-- ── Cybersecurity policy screen (13) ─────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'cyber.title',
 N'Cybersecurity policies and controls',
 N'سياسات وضوابط الأمن السيبراني'),
(N'cyber.intro',
 N'The SIMF mobile application complies with the cybersecurity policies and controls issued by the National Cybersecurity Authority (NCA), based on the Essential Cybersecurity Controls (ECC – 1:2018) and the Critical Systems Cybersecurity Controls (CSCC – 1:2019).',
 N'يلتزم تطبيق الملتقى البحري السعودي الدولي بسياسات وضوابط الأمن السيبراني الصادرة عن الهيئة الوطنية للأمن السيبراني (NCA)، استناداً إلى الضوابط الأساسية للأمن السيبراني (ECC – 1:2018) وضوابط الأمن السيبراني للأنظمة الحساسة (CSCC – 1:2019).'),
(N'cyber.pillar.01.title',
 N'Personal data protection and privacy',
 N'حماية البيانات الشخصية والخصوصية'),
(N'cyber.pillar.01.body',
 N'Data is collected for specified purposes only and retained under approved policies.',
 N'جمع البيانات لأغراض محددة فقط، وحفظها وفق الأنظمة المعتمدة'),
(N'cyber.pillar.02.title',
 N'Encryption and communications protection',
 N'التشفير وحماية الاتصالات'),
(N'cyber.pillar.02.body',
 N'Data is encrypted in transit and at rest using approved standards.',
 N'تشفير البيانات أثناء النقل والتخزين باستخدام معايير معتمدة'),
(N'cyber.pillar.03.title',
 N'Access and authentication controls',
 N'ضوابط الوصول والمصادقة'),
(N'cyber.pillar.03.body',
 N'Multi-factor authentication and least-privilege are enforced.',
 N'المصادقة متعددة العوامل ومبدأ أقل صلاحية لازمة'),
(N'cyber.pillar.04.title',
 N'Security review and testing',
 N'مراجعة واختبار الأمن'),
(N'cyber.pillar.04.body',
 N'Penetration tests and vulnerability assessments before launch and on every update.',
 N'اختبارات اختراق وتقييم ثغرات قبل الإطلاق وعند كل تحديث'),
(N'cyber.pillar.05.title',
 N'Incident reporting and response',
 N'الإبلاغ عن الحوادث والاستجابة'),
(N'cyber.pillar.05.body',
 N'A documented reporting channel with a defined response time for handling incidents.',
 N'قناة موثقة للإبلاغ وزمن استجابة محدد لمعالجة الحوادث'),
(N'cyber.reference',
 N'References: National Cybersecurity Authority · ECC – 1:2018 · CSCC – 1:2019 · OWASP ASVS',
 N'مرجعية: الهيئة الوطنية للأمن السيبراني · ECC – 1:2018 · CSCC – 1:2019 · OWASP ASVS');

-- ── Landing hero (7) ─────────────────────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'hero.titlestart',
 N'The future of',
 N'مستقبل أمن'),
(N'hero.titlehighlight',
 N'seabed security',
 N'قاع البحار'),
(N'hero.titleend',
 N'and global supply chains',
 N'وسلاسل الإمداد العالميّة'),
(N'hero.tagline',
 N'A global Saudi platform bringing leaders, decision-makers and experts together to shape the future of maritime security and protect vital corridors amid accelerating geopolitical and technological change.',
 N'منصّة سعوديّة عالميّة تجمع القادة وصنّاع القرار والخبراء لاستشراف مستقبل الأمن البحري وحماية الممرّات الحيوية في ظل التحولّات الجيوسياسيّة والتقنيّة المتسارعة.'),
(N'hero.metadate',
 N'23-25 November 2026',
 N'23-25 نوفمبر 2026'),
(N'hero.metavenue',
 N'Sofitel Riyadh Hotel & Convention Centre',
 N'فندق ومركز مؤتمرات سوفيتيل الرياض'),
(N'hero.ctasecondary',
 N'Browse the programme',
 N'تصفّح البرنامج');

-- ── Landing: About (4) ───────────────────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'about.eyebrow',
 N'About the Forum',
 N'حول الملتقى'),
(N'about.h2',
 N'A Saudi global platform driving dialogue and cooperation on maritime security',
 N'منصة سعودية عالمية لدعم الحوار والتعاون في قضايا الأمن البحري'),
(N'about.p1',
 N'The Saudi International Maritime Forum is a high-level event that brings together leaders, officials, and experts to share experience and build a shared global understanding of the future of maritime security amid accelerating geopolitical and technological change.',
 N'الملتقى البحري السعودي الدولي حدث رفيع المستوى يجمع القادة والمسؤولين والخبراء لتبادل التجارب والخبرات، وتعزيز فهم عالمي مشترك لمستقبل الأمن البحري في ظل التحولات الجيوسياسية والتقنية المتسارعة.'),
(N'about.p2',
 N'The Forum reflects the Kingdom of Saudi Arabia''s strategic role in anchoring stability across the seas and supporting the resilience of the global economy through an integrated framework that protects the seabed and enhances the efficiency of energy and trade supply chains.',
 N'يعكس الملتقى الدور الاستراتيجي للمملكة العربية السعودية في ترسيخ استقرار البحار ودعم استدامة الاقتصاد العالمي، عبر منظومة متكاملة لحماية قاع البحار ورفع كفاءة سلاسل إمداد الطاقة والتجارة.');

-- ── Landing: global-landscape stats strip (11) ───────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'stats.eyebrow',
 N'Global Landscape',
 N'المشهد العالمي'),
(N'stats.intro',
 N'The world is witnessing unprecedented shifts in maritime security. As threats to global supply chains escalate, seabed security emerges as an urgent international priority for stabilising the seas and sustaining the global economy.',
 N'يشهد العالم تحولات غير مسبوقة في أمن البحار، ومع تصاعد التهديدات التي تطال سلاسل الإمداد العالمية، يبرز أمن قاع البحار كأولوية دولية ملحة لتعزيز استقرار البحار وضمان استدامة الاقتصاد العالمي.'),
(N'stats.h3',
 N'A progressive path tracking the shifts in global maritime security',
 N'مسار متدرج يواكب تحولات الأمن البحري العالمي'),
(N'stats.count1', N'500', N'500'),
(N'stats.label1', N'Participating countries', N'دولة مشاركة'),
(N'stats.count2', N'220', N'220'),
(N'stats.label2', N'Leaders & officials', N'قائد ومسؤول'),
(N'stats.count3', N'100', N'100'),
(N'stats.label3', N'International speakers', N'متحدث دولي'),
(N'stats.count4', N'40', N'40'),
(N'stats.label4', N'Sessions & dialogues', N'جلسة وحوار');

-- ── Landing: Pillars header (3) ──────────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'pillars.eyebrow', N'Key Pillars', N'المحاور الرئيسية'),
(N'pillars.h2',      N'Key Pillars', N'المحاور الرئيسية'),
(N'pillars.p',
 N'Building a comprehensive strategic vision that addresses energy systems, trade, and the link between surface and depths through five core pillars that anchor maritime security and global economic stability.',
 N'لصياغة رؤية استراتيجية شاملة تعالج منظومات الطاقة والتجارة والاتصال بين السطح والأعماق عبر خمسة محاور رئيسية تشكل ركائز الأمن البحري واستقرار الاقتصاد العالمي.');

-- ── Landing: Goals (14) ──────────────────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'goals.eyebrow', N'Forum Goals', N'أهداف الملتقى'),
(N'goals.h2',      N'Ambitious Goals', N'أهداف طموحة'),
(N'goals.p',
 N'Building an integrated maritime security framework that supports international efforts to protect the seabed and enhance supply-chain efficiency, contributing to global economic stability in alignment with Saudi Vision 2030.',
 N'تعزيز منظومة أمن بحري متكاملة تدعم الجهود الدولية لحماية قاع البحار ورفع كفاءة سلاسل الإمداد، بما يسهم في استقرار الاقتصاد العالمي ويتّسق مع مستهدفات رؤية المملكة 2030.'),
(N'goals.btn', N'Browse all goals', N'تصفّح الأهداف الكاملة'),
(N'goals.item1.t',
 N'Strengthen regional and international maritime security',
 N'تعزيز الأمن البحري الإقليمي والدولي'),
(N'goals.item1.d',
 N'Unifying efforts to protect vital maritime corridors and ensure the stability of global navigation.',
 N'توحيد الجهود لحماية الممرّات البحرية الحيويّة وضمان استقرار حركة الملاحة العالميّة.'),
(N'goals.item2.t',
 N'Protect subsea infrastructure',
 N'حماية البنية التحتيّة تحت السطح'),
(N'goals.item2.d',
 N'Safeguarding cables, energy lines, and pipelines that connect the global economy beneath the sea.',
 N'صون الكابلات وخطوط الطاقة والأنابيب التي تربط الاقتصاد العالمي تحت قاع البحار.'),
(N'goals.item3.t',
 N'Enhance supply-chain efficiency',
 N'رفع كفاءة سلاسل الإمداد'),
(N'goals.item3.d',
 N'Modernising ports, corridors, and shipping systems to increase resilience and reduce risk.',
 N'تطوير منظومات الموانئ والممرّات وأنظمة الشحن لرفع المرونة وتقليل المخاطر.'),
(N'goals.item4.t',
 N'Exchange knowledge and build capacity',
 N'تبادل المعرفة وبناء القدرات'),
(N'goals.item4.d',
 N'Expanding the knowledge platform between leaders and experts to develop national and international talent.',
 N'توسيع منصّة المعرفة بين القادة والخبراء لصقل الكوادر الوطنيّة والدوليّة.'),
(N'goals.item5.t',
 N'Contribute to Vision 2030',
 N'الإسهام في تحقيق رؤية 2030'),
(N'goals.item5.d',
 N'Strengthening the Kingdom''s position as a global hub for maritime security and the blue economy.',
 N'تعزيز موقع المملكة قطبًا عالميًّا في الأمن البحري والاقتصاد الأزرق.');

-- ── App: Terms and About (2) ─────────────────────────────────────────
INSERT INTO @blocks (BlockKey, Content, ContentArabic) VALUES
(N'terms', @termsEn, @termsAr),
(N'about', @aboutEn, @aboutAr);

/* ---------------------------------------------------------------------
   Insert only the keys that are absent. A block an admin has edited (or
   deliberately deactivated) is never overwritten.
   --------------------------------------------------------------------- */
INSERT INTO dbo.ContentBlocks (Id, [Key], Content, ContentArabic, IsActive,
    LastUpdatedByUserId, CreatedAt, LastUpdatedAt)
SELECT NEWID(), b.BlockKey, b.Content, b.ContentArabic, 1,
       @sys, @now, @now
  FROM @blocks b
 WHERE NOT EXISTS (SELECT 1 FROM dbo.ContentBlocks c WHERE c.[Key] = b.BlockKey);

COMMIT TRANSACTION;
