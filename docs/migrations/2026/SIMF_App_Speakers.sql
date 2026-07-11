/* =====================================================================
   SIMF_App — SPEAKERS seed (SIMF-4 2026 proposed speaker roster)

   Source          : docs deck  15-04-2024/3قائمة المتحدثين.pptx
                     ("Proposed speakers list, 4th Saudi International
                      Maritime Forum 2026"). 32 speakers, 7 topic sections.
   Decision        : D-718 (content data = manual SQL; see README.md alongside).
   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every INSERT is guarded by IF NOT EXISTS (Speakers
                     on Code, Countries on Id). Re-running adds nothing new.
   Transactional   : whole script is one transaction with SET XACT_ABORT ON,
                     so ANY error rolls the whole thing back — no partial data.

   NOTE (real, UNCONFIRMED people): the deck marks every profile
   "متحدث مدعو — لم يتم تأكيد المشاركة بعد | المعلومات مصدرها السجلات العامة"
   (invited; participation NOT yet confirmed; sourced from public records).
   Run this ONLY when the roster is confirmed for publication. English names
   are best-effort transliterations from the profile URLs.

   Photos          : this file seeds TEXT only (PhotoRelativePath left NULL).
                     The 23 real headshots are seeded separately via the
                     centralized StoredFile store — run
                     SIMF_App_SpeakerPhotos.sql AFTER this file and deploy the
                     speaker-photos/speakerphoto folder into the API
                     file-storage root (prod: C:\SIMF\Storage\files). Speakers
                     without a deck photo (9 of 32) get none — admin uploads later.

   Consent flags   : AllowsMeetingRequests = 0 (do not open meeting requests to
                     an unconfirmed speaker; an admin enables per speaker).
                     AllowsDataSharing = 1 so the public profile surfaces the
                     public-record profile URL (the deck's stated source).
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

/* ---------------------------------------------------------------------
   0) Missing country lookups — Poland (616) and Tunisia (788) are the only
   two SIMF-4 nationalities not in the CountryConfiguration seed. Added here
   as guarded data rows (equivalent to an admin adding them via the CP CRUD;
   no schema change, no migration — respects the D-110 freeze).
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Countries WHERE Id = 616)
    INSERT INTO dbo.Countries (Id, Code, Name, NameArabic, PhonePrefix, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (616, N'PL', N'Poland',  N'بولندا', N'+48',  305, 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.Countries WHERE Id = 788)
    INSERT INTO dbo.Countries (Id, Code, Name, NameArabic, PhonePrefix, DisplayOrder, IsActive, CreatedAt, CreatedBy)
    VALUES (788, N'TN', N'Tunisia', N'تونس',  N'+216', 105, 1, @now, @sys);

/* ---------------------------------------------------------------------
   1) SPEAKERS — 32 rows, grouped by the deck's 7 topic sections.
   Columns set: Code, Name (EN transliteration), NameArabic (authoritative),
   Rank (EN role), CountryId (FK), Bio + BioArabic, one profile URL routed to
   WebsiteUrl / LinkedInUrl / XUrl, DisplayOrder (10..320, section order),
   consent flags, audit. PhotoRelativePath left NULL (admin uploads later).
   --------------------------------------------------------------------- */

-- Section 1: Senior naval military leadership  |  كبار القادة العسكريين البحريين
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-01')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-01', N'Rear Adm. Prof. Tomasz Zubrzycht', N'اللواء البحري البروفيسور توماش زوبريشت', N'Commander, Polish Naval Academy', 616,
        N'Leadership–academic integration: holds the rank of Rear Admiral, bridging naval command and higher military education. · A distinguished researcher with a PhD in science and the academic title of Professor. · Deep specialist in maritime security, naval tactics, asymmetric maritime threats and command systems. · A prolific author whose research shapes modern Polish naval doctrine and defence policy.',
        N'تكامل قيادي وأكاديمي: يحمل رتبة لواء بحري ويربط بين القيادة البحرية والتعليم العالي العسكري. · باحث متميز حاصل على درجة الدكتوراه في العلوم ويحمل الدرجة العلمية بروفيسور. · متخصص بعمق في الأمن البحري والتكتيكات البحرية والتهديدات البحرية غير المتكافئة وأنظمة القيادة. · مؤلف غزير الإنتاج تعمل أبحاثه على تشكيل العقيدة البحرية البولندية الحديثة وسياسة الدفاع.',
        N'https://old.amw.gdynia.pl/index.php/en/', NULL, NULL, 10,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-02')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-02', N'Capt. (Retd) Dr. Gurpreet S. Khurana', N'العقيد البحري (متقاعد) الدكتور جوربريت إس كورانا', N'Maritime security researcher & analyst', 356,
        N'Retired naval commander, academic and researcher/analyst in maritime security. · A pioneer of strategic analysis of the Indo-Pacific maritime domain. · Former Executive Director of India''s National Maritime Foundation and author of several books and papers on maritime security. · A leading analyst of Chinese maritime strategy and competition in the Indian Ocean. · Expert in the application of the law of the sea in the contested waters of the Indo-Pacific.',
        N'قائد بحري متقاعد وأكاديمي وباحث ومحلل في مجال الأمن البحري. · رائد في التحليل الاستراتيجي للمجال البحري في منطقة المحيطين الهندي والهادئ. · المدير التنفيذي السابق للهيئة البحرية الوطنية الهندية ومؤلف لعدد من الكتب والأوراق البحثية في مجال الأمن البحري. · محلل بارز في مجال الاستراتيجية البحرية الصينية والمنافسة في المحيط الهندي. · خبير في تطبيقات القانون البحري في المياه المتنازع عليها في منطقة المحيطين الهندي والهادئ.',
        N'https://nalandauniv.edu.in/faculties/captain-dr-gurpreet-s-khurana/', NULL, NULL, 20,
        0, 1, @now, @sys, 1);

-- Section 2: Maritime strategies  |  الاستراتيجيات البحرية
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-03')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-03', N'Dr. Christina Schori Liang', N'الدكتورة كريستينا شوري ليانغ', N'Head of Terrorism & PVE, GCSP', 756,
        N'Expert on terrorism, violent extremism and maritime security; heads the counter-terrorism and preventing-violent-extremism programme at the Geneva Centre for Security Policy. · Maritime terrorism: analysis of terrorist threats to vital sea lanes and port infrastructure. · Leads Geneva''s renowned security dialogue platform. · Research on how extremist groups exploit cyberspace to create security disruption. · Advises the UN, the EU and governments on counter-terrorism policy.',
        N'خبيرة في شؤون الإرهاب والتطرف والأمن البحري وترأس برنامج مكافحة الإرهاب ومنع التطرف في مركز جنيف للسياسات الأمنية. · الإرهاب البحري: تحليل التهديدات الإرهابية للممرات البحرية الحيوية والبنية التحتية للموانئ. · تقود منصة نقاشات جنيف الشهيرة للأمن. · أبحاث حول كيفية استغلال الجماعات المتطرفة للفضاء الإلكتروني لإحداث اضطرابات أمنية. · تقدم الاستشارات للأمم المتحدة والاتحاد الأوروبي والحكومات بشأن سياسة مكافحة الإرهاب.',
        N'https://www.gcsp.ch/experts/dr-christina-schori-liang', NULL, NULL, 30,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-04')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-04', N'Dr. Alessio Patalano', N'الدكتور أليسيو باتالانو', N'Professor of War & Strategy, KCL', 826,
        N'Expert in maritime strategy and professor of war and strategy in East Asia. · East Asia focus: expert in Japanese, Chinese and Korean maritime strategy. · Professor in the prestigious Department of War Studies at King''s College London and a fellow at RUSI. · Advises the United Kingdom and allied governments on maritime security in East Asia and on naval strategy.',
        N'خبير في الاستراتيجية البحرية وأستاذ الحرب والاستراتيجية في شرق آسيا. · التركيز على شرق آسيا: خبير في الاستراتيجية البحرية اليابانية والصينية والكورية. · أستاذ في قسم دراسات الحرب المرموق بجامعة كينجز كوليدج لندن وباحث في معهد RUSI. · يقدم الاستشارات للمملكة المتحدة وحكومات الحلفاء بشأن الأمن البحري في شرق آسيا والاستراتيجيات البحرية.',
        N'https://www.kcl.ac.uk/people/dr-alessio-patalano', NULL, NULL, 40,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-05')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-05', N'Dr. Sugio Takahashi', N'الدكتور سوجيو تاكاهاشي', N'Head, Defense & Security Studies, NIDS', 392,
        N'Expert in defence and security strategy; head of the Defense and Security Studies division at Japan''s National Institute for Defense Studies. · An authority on Japanese national-security strategy and naval-defence policy. · Expert in the evolving naval and military dimensions of the US–Japan alliance. · Analysis and research on naval competition and crisis management in the East China Sea.',
        N'خبير الاستراتيجيات الدفاعية والأمنية ورئيس قسم دراسات الدفاع والأمن في المعهد الوطني للدراسات الدفاعية في اليابان. · مرجع في استراتيجية الأمن القومي الياباني وسياسة الدفاع البحري. · خبير في الأبعاد البحرية والعسكرية المتطورة للتحالف الأمريكي الياباني. · تحليل وأبحاث المنافسة البحرية وإدارة الأزمات في بحر الصين الشرقي.',
        N'https://www.nids.mod.go.jp/english/researchfellow/anzen/06-takahashi.html', NULL, NULL, 50,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-06')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-06', N'Prof. Sanjay Chaturvedi', N'البروفيسور سانجاي شاتورفيدي', N'Professor, South Asian University', 356,
        N'Expert in international relations in the field of maritime security; former director of an international-relations research centre at the Institute of South Asian Studies. · Numerous publications on maritime affairs and maritime disputes. · Academic experience across several countries in Asia, Australia and Europe. · Participation in international committees on the resolution of maritime disputes.',
        N'خبير العلاقات الدولية في مجال الأمن البحري ومدير مركز دراسات سابق في مجال العلاقات الدولية في معهد دراسات جنوب آسيا. · مؤلفات عديدة في المجال البحري والنزاعات البحرية. · خبرة أكاديمية في عدد من دول العالم في آسيا وأستراليا وأوروبا. · عدد من المشاركات في اللجان الدولية في مجال فض النزاعات البحرية.',
        N'https://sau.int/faculty/sanjay-chaturvedi/', NULL, NULL, 60,
        0, 1, @now, @sys, 1);

-- Section 3: Maritime cybersecurity  |  الأمن السيبراني البحري
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-07')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-07', N'Dr. Omaimah Omar Bamasag', N'د. أميمة بنت عمر بامسق', N'Deputy Governor for Enablement, Transport Authority', 682,
        N'Cybersecurity expert and enablement leader at the Transport General Authority, and one of Saudi Arabia''s national leaders in cybersecurity. · PhD in cybersecurity from the University of Manchester (UK) and an Ibn Khaldun Fellow at MIT for Saudi women leaders. · Over 27 years of academic experience, including serving as vice-dean of the College of Computing at King Abdulaziz University. · Currently leads the enablement sector at the Transport General Authority.',
        N'خبيرة الأمن السيبراني وقائدة التمكين في الهيئة العامة للنقل ومن القيادات الوطنية السعودية في مجال الأمن السيبراني. · درجة الدكتوراه في الأمن السيبراني من جامعة مانشستر ببريطانيا وحاصلة على زمالة ابن خلدون البحثية في جامعة MIT للقيادات السعودية النسائية. · خبرة أكاديمية تمتد لأكثر من 27 عاماً عملت خلالها وكيلة لكلية الحاسبات بجامعة الملك عبدالعزيز. · تقود حالياً قطاع التمكين في الهيئة العامة للنقل.',
        NULL, N'https://linkedin.com/in/omaimahbamasag', NULL, 70,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-08')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-08', N'Dr. Sadie Creese', N'الدكتورة سادي كريز', N'Professor of Cybersecurity, Univ. of Oxford', 826,
        N'A pioneer of cybersecurity at Oxford and professor of cybersecurity at a centre ranked first worldwide. · Directs the Global Centre of Excellence for cyber-capacity building at the University of Oxford. · Develops frameworks for assessing cyber risk to the UK''s critical infrastructure, such as seaports. · Advises the UK government, the ITU and Commonwealth states on national cybersecurity strategy.',
        N'رائدة الأمن السيبراني في أكسفورد وأستاذة الأمن السيبراني في مركز مصنف الأول عالمياً. · تدير المركز العالمي للتميز لبناء القدرات في مجال الأمن السيبراني بجامعة أكسفورد. · تطوير أطر عمل لتقييم المخاطر السيبرانية للبنية التحتية الحيوية في المملكة المتحدة مثل الموانئ البحرية. · تقدم المشورة لحكومة المملكة المتحدة والاتحاد الدولي للاتصالات ودول الكومنولث بشأن الاستراتيجية الوطنية للأمن السيبراني.',
        N'https://www.cs.ox.ac.uk/people/sadie.creese/', NULL, NULL, 80,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-09')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-09', N'Dr. Kashif Naseer', N'د. كاشف ناصر', N'Researcher, University of Limerick, Ireland', 586,
        N'Researcher in cybersecurity and artificial intelligence at the University of Limerick, Ireland. · Won the Best Researcher Award for consecutive years (2020–2022) in recognition of his high-impact research. · Published more than 170 papers in prestigious international journals with a strong impact factor; his work has earned thousands of citations on platforms such as Google Scholar.',
        N'باحث في مجال الأمن السيبراني والذكاء الاصطناعي بجامعة ليمريك في جمهورية أيرلندا. · حصل على جائزة أفضل باحث (Best Researcher Award) لسنوات متتالية من 2020 إلى 2022 تقديراً لأبحاثه ذات الأثر العالي. · نشر أكثر من 170 ورقة بحثية في مجلات علمية دولية مرموقة ذات معامل تأثير قوي وحصدت أبحاثه آلاف الاقتباسات على منصات مثل Google Scholar.',
        NULL, N'https://www.linkedin.com/in/kashifnq/', NULL, 90,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-10')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-10', N'Prof. Gary Corn', N'البروفيسور غاري كورن', N'Colonel (Retd), US Army; cyber strategist', 840,
        N'Expert in the law of military cyber warfare and cyber strategy, and a retired US Army colonel. · A recognised authority on the law of cyber operations and military cyber doctrine. · Practical experience in US Army Cyber Command and service at the Pentagon. · Helped shape the US military''s legal framework for cyber attack and defence. · Combines operational military experience with legal scholarship in cybersecurity.',
        N'خبير قانون الحروب السيبرانية العسكرية والاستراتيجيات السيبرانية وعقيد متقاعد في الجيش الأمريكي. · مرجع معتمد في قانون العمليات السيبرانية والعقيدة السيبرانية العسكرية. · خبرة عملية في قيادة الفضاء الإلكتروني للجيش الأمريكي والخدمة في البنتاغون. · ساهم في تشكيل الإطار القانوني للجيش الأمريكي للهجمات الإلكترونية والدفاع عنها. · يجمع بين الخبرة العسكرية العملياتية والدراسات القانونية في مجال الأمن السيبراني.',
        N'https://www.american.edu/wcl/faculty/gcorn.cfm', NULL, NULL, 100,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-11')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-11', N'Dr. Ibrahim Tariq Javed', N'الدكتور إبراهيم طارق جافد', N'Researcher, Univ. of the Fraser Valley, Canada', 586,
        N'Researcher and academic in cybersecurity and emerging technologies at the University of the Fraser Valley, Canada. · Prior academic experience as a cybersecurity researcher at a naval university in Pakistan. · Several papers published in Q1 journals. · Research and academic experience across several countries (France, Ireland and Canada).',
        N'باحث وأكاديمي في مجال الأمن السيبراني والتقنيات الحديثة بجامعة فريزر فالي في كندا. · خبرة أكاديمية سابقة في جامعة بحرية في جمهورية باكستان كباحث أمن سيبراني. · عدد من الأبحاث المنشورة في مجلات علمية من الفئة Q1. · خبرة في المجال البحثي والأكاديمي في عدد من الدول (فرنسا وأيرلندا وكندا).',
        NULL, N'https://linkedin.com/in/ibrahim-tariq-javed-phd-88892544', NULL, 110,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-12')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-12', N'Assoc. Prof. Doğrul Mürsel', N'الدكتور دوقرول مورسيل', N'Assoc. Professor, Turkish National Defense Univ.', 792,
        N'Associate professor at the Turkish National Defense University, specialising in international relations, security studies and Asia-Pacific affairs. · His research focuses in particular on Japanese foreign policy, the political economy of energy, cybersecurity and science diplomacy.',
        N'أستاذ مشارك في جامعة الدفاع الوطنية التركية ويتخصص في العلاقات الدولية ودراسات الأمن وشؤون منطقة آسيا والمحيط الهادئ. · تركز أبحاثه بشكل خاص على السياسة الخارجية اليابانية والاقتصاد السياسي للطاقة والأمن السيبراني ودبلوماسية العلوم.',
        N'https://msu.edu.tr/eng/sayfadetail.aspx?SayfaId=1455&ParentMenuId=8', NULL, NULL, 120,
        0, 1, @now, @sys, 1);

-- Section 4: International maritime law  |  القانون البحري الدولي
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-13')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-13', N'Prof. Robert McLaughlin', N'البروفيسور روبرت ماكلافلين', N'Professor of Int''l Maritime Law, ANU', 36,
        N'Expert in international maritime law and maritime security, and professor of international maritime law at the Australian National University. · A globally recognised expert on the UN Convention on the Law of the Sea. · Author of numerous works on maritime disputes. · A retired naval-law officer of the Royal Australian Navy. · Member of the maritime counter-piracy committee at the International Maritime Organization.',
        N'خبير القانون البحري الدولي والأمن البحري وأستاذ القانون الدولي البحري في الجامعة الوطنية الأسترالية. · خبير معترف به عالمياً في اتفاقية الأمم المتحدة لقانون البحار. · نُشرت له العديد من المؤلفات حول النزاعات البحرية. · ضابط قانون بحري متقاعد من البحرية الأسترالية. · عضو لجنة مكافحة القرصنة البحرية في المنظمة البحرية الدولية.',
        N'https://law.anu.edu.au/about/our-people/robert-mclaughlin', NULL, NULL, 130,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-14')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-14', N'Prof. Christian Bueger', N'البروفيسور كريستيان بوغر', N'Professor of Int''l Relations & Maritime Security', 276,
        N'Expert in maritime security and maritime policy, and professor of international relations and maritime security. · Formulated and developed the concept of ''maritime security'' as an academic field. · Leads research on maritime governance in the Indian Ocean and counter-piracy frameworks. · Authored reference studies on piracy in the Gulf of Aden and counter-piracy systems. · Research on ocean governance and the sustainable blue economy.',
        N'خبير الأمن البحري والسياسات البحرية وأستاذ العلاقات الدولية والأمن البحري. · قام بصياغة وتطوير مفهوم «الأمن البحري» كمجال أكاديمي. · يقود الأبحاث المتعلقة بالحوكمة البحرية في المحيط الهندي وأطر مكافحة القرصنة. · ألّف دراسات مرجعية حول القرصنة في خليج عدن وأنظمة مكافحة القرصنة. · أبحاث حول إدارة المحيطات والاقتصاد الأزرق المستدام.',
        N'https://bueger.info/', NULL, NULL, 140,
        0, 1, @now, @sys, 1);

-- Section 5: Technology & artificial intelligence  |  التكنولوجيا والذكاء الاصطناعي
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-15')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-15', N'Prof. Lam Kwok Yan', N'الأستاذ لام كوك يان', N'Professor of Computer Science, NTU Singapore', 702,
        N'Expert in AI and emerging technologies for the maritime domain and professor of computer science at Nanyang Technological University (NTU), Singapore. · His cyber research focuses on cryptography, network security and AI-powered cyber defence. · Contributes to smart-port research and Singapore''s digital maritime ecosystem. · Research on securing supply chains via blockchain and AI. · Multiple awards and an extensive publication record at leading IEEE conferences.',
        N'خبير الذكاء الاصطناعي والتقنيات الحديثة في المجال البحري وأستاذ علوم الحاسوب بجامعة نانيانغ التكنولوجية (NTU) في سنغافورة. · تركز أبحاثه السيبرانية على التشفير وأمن الشبكات والدفاعات السيبرانية المدعومة بالذكاء الاصطناعي. · يساهم في أبحاث الموانئ الذكية والنظام البيئي البحري الرقمي في سنغافورة. · أبحاث في تأمين سلاسل الإمداد بواسطة تقنيات البلوكشين والذكاء الاصطناعي. · جوائز متعددة وسجل نشر واسع في أبرز مؤتمرات IEEE.',
        N'https://dr.ntu.edu.sg/entities/person/Lam-Kwok-Yan', NULL, NULL, 150,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-16')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-16', N'Mike Constable', N'مايك كونستبل', N'Digital-infrastructure expert, Infra Analysis', 554,
        N'Expert in digital infrastructure at Infra Analysis, Singapore. · Over 30 years of pioneering experience; one of the world''s leading experts in digital infrastructure and submarine fibre-optic cable networks, having held senior executive (C-level) roles on both the investment and service-provider sides. · Vice-chair of the UN Joint Task Force steering committee for SMART cables, an executive-committee member of the global SubOptic association, and a graduate of Harvard Business School''s Advanced Management Program.',
        N'خبير في قطاع البنية التحتية الرقمية بشركة إنفرا للتحليل في سنغافورة. · خبرة رائدة لأكثر من 30 عاماً ويُعد أحد أبرز الخبراء عالمياً في البنية التحتية الرقمية وشبكات كابلات الألياف الضوئية البحرية وقاد مناصب تنفيذية عليا على مستويي الاستثمار ومزودي الخدمة. · نائب رئيس اللجنة التوجيهية المشتركة لكابلات SMART التابعة للأمم المتحدة وعضو اللجنة التنفيذية لرابطة SubOptic العالمية وخريج برنامج الإدارة المتقدمة من كلية هارفارد للأعمال.',
        NULL, N'https://www.linkedin.com/in/mike-constable-5979021/', NULL, 160,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-17')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-17', N'Prof. Mohamed-Slim Alouini', N'البروفيسور محمد سليم علويني', N'Distinguished Professor, KAUST', 788,
        N'Expert in maritime and satellite wireless-communications research and distinguished professor of electrical engineering at King Abdullah University of Science and Technology (KAUST). · A distinguished professor with more than 750 publications and over 45,000 citations in satellite and wireless communications. · Holder of the UNESCO chair in digital transformation. · A research leader at KAUST supervising work on connectivity solutions for under-served maritime areas. · An IEEE Fellow and winner of numerous international research awards.',
        N'خبير أبحاث الاتصالات اللاسلكية البحرية والفضائية وأستاذ متميز في الهندسة الكهربائية بجامعة الملك عبدالله للعلوم والتقنية (كاوست). · أستاذ متميز بأكثر من 750 منشوراً وأكثر من 45000 استشهاد في مجال الاتصالات الفضائية واللاسلكية. · رئيس كرسي التحول الرقمي في اليونيسكو. · قائد بحثي في جامعة كاوست يشرف على أبحاث حلول الاتصال في المناطق البحرية المحرومة من الخدمات. · عضو خبير في معهد IEEE وحائز على العديد من جوائز البحث الدولية.',
        N'https://www.kaust.edu.sa/en/study/faculty/mohamed-slim-alouini', NULL, NULL, 170,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-18')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-18', N'Prof. Tareq Y. Al-Naffouri', N'البروفيسور طارق يوسف النافوري', N'Professor of Electrical & Computer Eng., KAUST', 682,
        N'Expert in maritime communications and sensing research and professor of electrical and computer engineering at King Abdullah University of Science and Technology. · PhD from Stanford University (2004) in wireless communications. · Leads research on positioning, sensing and communications for maritime navigation. · Integrates AI and machine learning into advanced communications and sensing systems. · An IEEE Fellow and winner of numerous international research awards.',
        N'خبير أبحاث الاتصالات البحرية والمستشعرات وأستاذ الهندسة الكهربائية وهندسة الحاسوب بجامعة الملك عبدالله للعلوم والتقنية. · دكتوراه من جامعة ستانفورد عام 2004 في تخصص الاتصالات اللاسلكية. · يقود أبحاثاً في تحديد المواقع والاستشعار والاتصالات لأغراض الملاحة البحرية. · يدمج الذكاء الاصطناعي والتعلم الآلي في أنظمة الاتصالات والاستشعار المتقدمة. · عضو خبير في معهد IEEE وحائز على العديد من جوائز البحث الدولية.',
        N'https://www.kaust.edu.sa/ar/study/faculty/tareq-alnaffouri', NULL, NULL, 180,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-19')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-19', N'Yasir Atalan', N'ياسر عطلان', N'Researcher, CSIS (global tech security)', 792,
        N'Researcher in global technology security at the Center for Strategic and International Studies (CSIS). · His expertise centres on the role of AI in foreign policy and defence, military technology, international and geopolitical security, and the changing nature of modern warfare, with a regional focus on the Middle East, Africa, Türkiye and NATO. · Publishes in-depth reports on the impact of emerging technologies in conflict, such as AI in mediation and conflict resolution and the effect of low-cost drones on contemporary military strategy.',
        N'باحث في الأمن التقني العالمي بمركز الدراسات الاستراتيجية والدولية. · تتركز خبراته حول دور الذكاء الاصطناعي في السياسة الخارجية والدفاع والتكنولوجيا العسكرية والأمن الدولي والجيوسياسي وتغير طبيعة الحروب الحديثة مع تركيز إقليمي على الشرق الأوسط وإفريقيا وتركيا وحلف الناتو. · ينشر تقارير ومقالات متعمقة حول تأثير التقنيات الحديثة في النزاعات مثل استخدام الذكاء الاصطناعي في الوساطة وحل النزاعات وتقييم تأثير الطائرات المسيرة منخفضة التكلفة على الاستراتيجيات العسكرية المعاصرة.',
        N'https://www.csis.org/people/yasir-atalan', NULL, NULL, 190,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-20')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-20', N'Dr. Basma Albuhairan', N'الدكتورة بسمة البحيران', N'Former Head, WEF C4IR (Saudi Arabia)', 682,
        N'An innovation leader and research adviser with extensive experience leading research-and-innovation centres in emerging technologies. · Director of the World Economic Forum''s Centre for the Fourth Industrial Revolution for a four-year term (2022–2026). · Currently an adviser to the President of King Abdulaziz City for Science and Technology.',
        N'قائدة في مجال الابتكار ومستشارة أبحاث بخبرة ممتدة في قيادة مراكز الأبحاث والابتكار في مجالات التقنيات الحديثة. · مديرة مركز الثورة الصناعية الرابعة التابع للمنتدى الاقتصادي العالمي في دورته (4 سنوات) من 2022 إلى 2026. · تعمل حالياً مستشارة رئيس مدينة الملك عبدالعزيز للعلوم والتقنية.',
        NULL, N'https://linkedin.com/in/basma-albuhairan-phd-78a32162', NULL, 200,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-21')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-21', N'Dr. Awatef Salem Balobaid', N'الدكتورة عواطف سالم بالعبيد', N'Associate Professor, Jazan University', 682,
        N'Researcher and research-project leader in maritime technologies and associate professor in the College of Computer Science at Jazan University. · Holds a PhD in computer science and engineering specialising in ''cloud computing''. · Leads research projects focused on emerging technologies for maritime purposes and seaport management.',
        N'باحثة وقائدة مشاريع أبحاث في التقنيات البحرية وأستاذ مشارك في كلية علوم الحاسب بجامعة جازان. · حاصلة على دكتوراه في علوم الحاسب والهندسة في تخصص «الحوسبة السحابية». · تقود مشاريع بحثية تركز على التقنيات الحديثة للأغراض البحرية وإدارة الموانئ البحرية.',
        NULL, NULL, N'https://x.com/DrBalobaid', 210,
        0, 1, @now, @sys, 1);

-- Section 6: Supply chains, logistics & the maritime economy  |  سلاسل الإمداد والخدمات اللوجستية والاقتصاد البحري
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-22')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-22', N'Dr. Ihsan M. Bu-Hulaiga', N'د. إحسان محمد بوحليقة', N'President, Joatha Consulting', 682,
        N'President of Joatha Consulting and a former member of the Shura Council. · An economist holding a PhD from the United States, he leads Joatha Consulting in economic studies and business strategy. · Served three consecutive terms in the Shura Council, chairing its Economic Affairs and Energy Committee, and represented the Kingdom in international parliamentary forums. · One of the most prominent economic voices in Saudi media, widely present in analysis of Vision 2030, development and digital transformation.',
        N'رئيس مركز جواثا الاستشاري وعضو سابق في مجلس الشورى. · خبير اقتصادي يحمل الدكتوراه من الولايات المتحدة ويقود مركز جواثا للاستشارات لتقديم الدراسات الاقتصادية واستراتيجيات الأعمال. · خدم لثلاث دورات متتالية في مجلس الشورى ورأس لجنة الشؤون الاقتصادية والطاقة ومثّل المملكة في محافل برلمانية دولية. · أحد أبرز الأصوات الاقتصادية في الصحافة السعودية وله حضور واسع في تحليل رؤية 2030 وقضايا التنمية والتحول الرقمي.',
        NULL, N'https://www.linkedin.com/in/ihsanbuhulaiga', NULL, 220,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-23')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-23', N'Mr. Saad Al-Qahtani', N'الأستاذ سعد القحطاني', N'Governance & info-confidentiality advisor', 682,
        N'Adviser on governance and government information confidentiality, and an expert in designing governance frameworks for government bodies with a strong focus on data and official-document classification and defining confidentiality levels and secure information access. · Oversees the build-out of government compliance systems to enforce the highest standards of integrity and safeguarding of administrative secrets, and trains leaders on the authority matrix and the secure handling of sensitive documents. · Advanced skill in drafting public-sector regulations and internal policies that balance administrative transparency with the protection of cybersecurity and sovereign information, in line with the Digital Government Authority and the National Cybersecurity Authority.',
        N'مستشار حوكمة وسرية معلومات حكومية وخبير في تصميم أطر الحوكمة المخصصة للجهات الحكومية مع تركيز عالٍ على تصنيف البيانات والمستندات الرسمية وتحديد مستويات السرية والوصول الآمن للمعلومات. · يشرف على بناء أنظمة الالتزام الحكومية لضمان تطبيق أعلى معايير النزاهة وحفظ الأسرار الإدارية وتدريب القيادات على مصفوفة الصلاحيات وتداول الوثائق الحساسة بأمان. · يمتلك مهارة متقدمة في صياغة اللوائح والسياسات الداخلية للقطاع العام بما يوازن بين الشفافية الإدارية وحماية الأمن السيبراني والمعلومات السيادية تماشياً مع تشريعات هيئة الحكومة الرقمية والهيئة الوطنية للأمن السيبراني.',
        NULL, N'https://www.linkedin.com/in/saad-alqahtani-a7566a175/', NULL, 230,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-24')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-24', N'Prof. Ayman Sedqah Fadel', N'د. أيمن بن صدقه فاضل', N'Professor, University of Jeddah', 682,
        N'A Saudi academic specialising in economics and management; he earned his master''s and doctorate from the United Kingdom and rose through the academic ranks to the title of Professor, with numerous studies in crisis and disaster management and economic feasibility. · Held senior administrative posts at the University of Jeddah, including dean of student affairs and dean of the College of Economics and Administration, and was assigned dean of the College of Law. · A former member of the Saudi Shura Council for two consecutive terms; he chaired the Jeddah municipal council, the honorary-members board of Al-Ahli Saudi Club, and the board of the Zamazemah company.',
        N'أكاديمي سعودي متخصص في الاقتصاد والإدارة حصل على الماجستير والدكتوراه من بريطانيا وتدرّج في الرتب العلمية حتى نال درجة الأستاذية (بروفيسور) وله العديد من الأبحاث في إدارة الأزمات والكوارث والجدوى الاقتصادية. · شغل مناصب إدارية رفيعة في منظومة جامعة جدة منها عمادة شؤون الطلاب وعمادة كلية الاقتصاد والإدارة إضافة إلى تكليفه عميداً لكلية الحقوق. · عُيّن عضواً سابقاً في مجلس الشورى السعودي لدورتين متتاليتين وتولى رئاسة المجلس البلدي لمدينة جدة ورئاسة هيئة أعضاء الشرف بالنادي الأهلي السعودي ورئاسة مجلس إدارة شركة الزمازمة.',
        N'https://www.uj.edu.sa/ar', NULL, NULL, 240,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-25')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-25', N'Brig. Gen. Salman H. Al-Harbi', N'العميد سلمان بن حسين الحربي', N'National Defense University', 682,
        N'Holds influential leadership and teaching roles at the National Defense University (formerly the Armed Forces Command and Staff College), contributing to the preparation and development of military and civilian leaders in strategic planning and national security. · Focuses on developing defence thought and helping supervise the academic programmes and strategic exercises that build officers'' readiness for contemporary security challenges. · Represents the university at official occasions, symposia and graduation ceremonies of senior military courses, drawing on broad field and academic experience aligned with the Ministry of Defense''s development vision.',
        N'يشغل أدواراً قيادية وتعليمية مؤثرة في جامعة الدفاع الوطني (كلية القيادة والأركان للقوات المسلحة سابقاً) ويساهم في إعداد وتأهيل القادة العسكريين والمدنيين في مستويات التخطيط الاستراتيجي والأمن الوطني. · يركز على تطوير الفكر الدفاعي والمشاركة في الإشراف على البرامج الأكاديمية والتمارين الاستراتيجية التي تعزز جاهزية الضباط لمواجهة التحديات الأمنية المعاصرة. · يمثل الجامعة في العديد من المناسبات الرسمية والندوات وحفلات التخرج للدورات العسكرية العليا مستنداً إلى خبرة ميدانية وأكاديمية واسعة تتماشى مع رؤية تطوير وزارة الدفاع.',
        N'https://ndu.mod.gov.sa/#about-university', NULL, NULL, 250,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-26')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-26', N'Dr. Marios P. Efthymiopoulos', N'د. ماريوس بانايوتيس', N'Professor of Int''l & Strategic Security', 300,
        N'Expert in international security and geopolitical studies and professor of international and strategic security at Vytautas Magnus University in Lithuania. · Previously dean of the College of Security and Global Studies at the American University in the Emirates, a researcher and adviser at the Emirates Center for Strategic Studies and Research (ECSSR) in Abu Dhabi, chair of the department of history, politics and international studies at Neapolis University Pafos in Cyprus, and an adviser to the Cypriot geostrategic council. · Holds a PhD in political science from the University of Crete, Greece, and a diploma from the NATO Defense College in Rome, with internationally published work on cybersecurity, hybrid threats and regional and international security architecture.',
        N'خبير في مجالات الأمن الدولي والدراسات الجيوسياسية وأستاذ الأمن الدولي والاستراتيجي في جامعة فيتاوتاس ماغنوس في ليتوانيا. · عمل سابقاً عميداً لكلية الأمن والدراسات العالمية في الجامعة الأمريكية في الإمارات وباحثاً ومستشاراً في مركز الإمارات للدراسات والبحوث الاستراتيجية (ECSSR) في أبوظبي وترأس قسم التاريخ والسياسة والدراسات الدولية في جامعة نيابوليس بافوس في قبرص وعمل مستشاراً في المجلس الجيواستراتيجي القبرصي. · حاصل على الدكتوراه في العلوم السياسية من جامعة كريت في اليونان ويحمل دبلوم كلية الدفاع التابعة لحلف الناتو في روما وله كتب وبحوث منشورة عالمياً تركز على الأمن السيبراني والتهديدات الهجينة وهندسة الأمن الإقليمي والدولي.',
        N'https://www.sakharovcenter-vdu.eu/events/past-events/the-sixth-leonidas-donskis-conference/marios-panagiotis-efthymiopoulos/', NULL, NULL, 260,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-27')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-27', N'Cdre. John Aitken (Retd)', N'العميد جون أتكين (متقاعد)', N'Royal Navy (Retd); seabed-security expert', 826,
        N'Expert in seabed security and supply chains with the British Royal Navy. · Rose through the ranks to Commodore, serving as Deputy Director Submarines at Navy Command headquarters, responsible for undersea-warfare strategy and policy and the management of the fleet''s operational capabilities. · Awarded the Order of the British Empire in June 2024 in recognition of his contributions to raising the combat effectiveness of naval forces; he holds a degree in English language and literature and is a graduate of the Major Projects Leadership Academy at the University of Oxford.',
        N'خبير في أمن قاع البحار وسلاسل الإمداد بالبحرية الملكية البريطانية. · تدرّج في الرتب العسكرية حتى رتبة كومودور (عميد بحري) وشغل منصب نائب مدير قطاع الغواصات في مقر القيادة البحرية وكان مسؤولاً عن استراتيجيات وسياسات الحرب البحرية تحت الماء وإدارة القدرات التشغيلية للأسطول. · حصل على وسام الإمبراطورية البريطانية في يونيو 2024 تقديراً لإسهاماته في رفع الكفاءة القتالية للقوات البحرية وهو حاصل على درجة علمية في اللغة الإنجليزية وآدابها وخريج أكاديمية القيادة للمشاريع الكبرى بجامعة أكسفورد.',
        NULL, N'https://www.linkedin.com/in/andrew-john-aitken/', NULL, 270,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-28')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-28', N'Prof. Zaili Yang', N'البروفيسور زايلي يانغ', N'Chair of Maritime Transport, LJMU', 826,
        N'Expert in maritime risk and transport systems and head of maritime transport at Liverpool John Moores University. · Projects on analysing and modelling the safety, resilience and sustainability of maritime-transport and logistics networks. · Research on digital-twin, AI and data-analytics solutions in maritime operations for risk detection. · Led major UK Research Council projects on maritime safety and autonomy.',
        N'خبير المخاطر البحرية وأنظمة النقل ورئيس قسم النقل البحري في كلية النقل البحري بجامعة ليفربول جون مورس. · مشاريع تحليل ونمذجة السلامة والمرونة والاستدامة لشبكات النقل البحري واللوجستي. · بحث حلول التوائم الرقمية والذكاء الاصطناعي وتحليلات البيانات في العمليات البحرية لكشف المخاطر. · قاد مشاريع رئيسية لمجلس البحوث البريطاني حول السلامة البحرية والاستقلالية.',
        N'https://profiles.ljmu.ac.uk/3777-zaili-yang', NULL, NULL, 280,
        0, 1, @now, @sys, 1);

-- Section 7: Organisations & research centres  |  المنظمات ومراكز الأبحاث والدراسات
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-29')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-29', N'Maj. Gen. Fahad H. Al-Otaibi', N'اللواء الركن فهد حمد العتيبي', N'Director, MoD Strategic Defense Studies Center', 682,
        N'Chief executive of the Center for Strategic Defense Studies and Research, heading the centre affiliated with the Saudi Ministry of Defense. · Anticipates the future of defence through rigorous research and the production of policies presented to decision-makers in the Ministry of Defense. · Convenes meetings with strategic leaders and represents the ministry in international engagements. · Responsible for developing strategies and policies supporting national security and defence and the development of the armed-forces branches.',
        N'الرئيس التنفيذي لمركز الدراسات والأبحاث الاستراتيجية الدفاعية ويرأس المركز التابع لوزارة الدفاع السعودية. · استشراف مستقبل الدفاع عبر إجراء أبحاث رصينة وإنتاج سياسات تُقدَّم لصنّاع القرار في وزارة الدفاع. · عقد وتنظيم اللقاءات مع القادة الاستراتيجيين وتمثيل الوزارة في المشاركات الدولية. · مسؤولية تطوير الاستراتيجيات والسياسات الداعمة للأمن والدفاع الوطني وتطوير عمل أفرع القوات المسلحة.',
        NULL, NULL, NULL, 290,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-30')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-30', N'Dr. Faisal Al-Saaq', N'الدكتور فيصل الصعاق', N'Dean, College of Maritime Studies, KAU', 682,
        N'Dean of the College of Maritime Studies at King Abdulaziz University. · Supervises research programmes on the Red Sea environment, safety, security and the maritime industry. · Leads the college''s development to keep pace with the importance of maritime research.',
        N'عميد كلية الدراسات البحرية بجامعة الملك عبدالعزيز. · يشرف على برامج بحثية حول بيئة البحر الأحمر والسلامة والأمن والصناعة البحرية. · يقود عملية تطوير الكلية لمواكبة أهمية أبحاث المجال البحري.',
        NULL, N'https://linkedin.com/in/dr-faisal-alsaaq-phd-m-eng-b-sc-1b792640', NULL, 300,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-31')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-31', N'Mr. Abdulrahman I. Al-Fuzai', N'الأستاذ عبد الرحمن إبراهيم الفزيع', N'Expert researcher, Derasat (Bahrain)', 48,
        N'An expert researcher at the Bahrain Center for Strategic, International and Energy Studies (Derasat) — an independent think-tank founded in 2009 in the Kingdom of Bahrain that analyses strategic developments at the national, regional and international levels. · Over ten years of experience in political and military institutions in Bahrain, and served as a diplomat at the Bahraini Ministry of Foreign Affairs. · His research focuses on geopolitical and strategic issues, including regional security, diplomacy and alliances.',
        N'باحث خبير في مركز البحرين للدراسات الاستراتيجية والدولية والطاقة (دراسات) وهو مركز فكري وبحثي مستقل تأسس عام 2009 في مملكة البحرين ويُعنى بتحليل التطورات الاستراتيجية على الصعد الوطنية والإقليمية والدولية. · يتمتع بخبرة تزيد على عشر سنوات في مؤسسات سياسية وعسكرية بمملكة البحرين وعمل دبلوماسياً بوزارة الخارجية البحرينية. · تتركز أبحاثه على القضايا الجيوسياسية والاستراتيجية بما في ذلك الأمن الإقليمي والدبلوماسية والتحالفات.',
        N'https://www.derasat.org.bh/ar/', NULL, NULL, 310,
        0, 1, @now, @sys, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-32')
    INSERT INTO dbo.Speakers (Id, Code, Name, NameArabic, [Rank], CountryId,
        Bio, BioArabic, WebsiteUrl, LinkedInUrl, XUrl, DisplayOrder,
        AllowsMeetingRequests, AllowsDataSharing, CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'SIMF4-SPK-32', N'Dr. Fahad S. Al-Arabi Al-Harthi', N'د. فهد بن ساعد العرابي الحارثي', N'President, Asbar Center for Studies', 682,
        N'President of the Asbar Center for Studies and a prominent Saudi thinker and media figure. · One of the Kingdom''s leading media and cultural figures, having served as editor-in-chief of Al-Yamamah magazine and Al-Watan newspaper, with wide writing and intellectual contributions in local and Arab media. · Founder and president of the Asbar Center for Studies, Research and Media and president of the Asbar International Forum, playing an active role in leading developmental and knowledge initiatives and generating strategic ideas. · Held membership of the Saudi Shura Council for several terms, contributing to legislative and oversight work and representing the Kingdom at many international forums.',
        N'رئيس مركز أسبار للدراسات ومفكر وإعلامي سعودي بارز. · من أبرز القامات الإعلامية والثقافية في المملكة حيث شغل منصب رئيس تحرير مجلة «اليمامة» وجريدة «الوطن» وله إسهامات كتابية وفكرية واسعة في الصحافة المحلية والعربية. · مؤسس ورئيس مركز أسبار للدراسات والبحوث والإعلام ورئيس منتدى أسبار الدولي وله دور فعّال في قيادة المبادرات التنموية والمعرفية وتوليد الأفكار الاستراتيجية. · حظي بعضوية مجلس الشورى السعودي لعدة دورات وساهم في العمل التشريعي والرقابي ومثّل المملكة في العديد من المحافل والمؤتمرات الدولية.',
        N'https://asbar.com/about/', NULL, NULL, 320,
        0, 1, @now, @sys, 1);

COMMIT TRANSACTION;

/* Verification — expect 32 SIMF-4 speakers after a fresh run. */
SELECT COUNT(*) AS Simf4Speakers FROM dbo.Speakers WHERE Code LIKE 'SIMF4-SPK-%';
SELECT Id, Code FROM dbo.Countries WHERE Id IN (616, 788);
