/* =====================================================================
   SIMF_App — baseline LOOKUPS seed  (ProfileTypes · Interests · Organisations)

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every write is guarded (see the per-section notes).
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder — the eight
                     EnsureProfileTypeAsync calls, EnsureBaselineInterestsAsync
                     and EnsureBaselineOrganisationsAsync. The owner rule is that
                     IdentitySeeder keeps ONLY the identity bootstrap (the roles,
                     the permission catalogue and the single super-admin); every
                     other seed lives in this SQL lane, in one location, behind
                     one runner (Run_All_App_Seeds.sql).

   >>> RUN THIS BEFORE ANYONE REGISTERS. All three lookups are REQUIRED by the
       visitor profile save: the app picker demands 1-10 interests and one
       organisation, and a profile cannot exist without a profile type. An
       environment where any of the three tables is empty makes registration
       impossible, which is exactly what happened on the first production
       install.

   ── GUARD SEMANTICS differ per table, deliberately ────────────────────────
   • ProfileTypes  — per-NAME existence check, with NO IsActive filter, so a
                     type an admin deliberately deactivated is never
                     resurrected by a re-run (this mirrors the C# AnyAsync).
   • Interests     — whole-table-empty guard. Admins own the list at runtime;
                     a deliberate deletion must never be re-added on a re-run,
                     so this is NOT a per-row guard.
   • Organisations — empty-EXCEPT-the-catch-all guard. The "Other" row is
                     seeded by the EF model itself (Organisation.OtherId via
                     HasData), so this table is never truly empty on a fresh
                     database and a plain "is it empty" test would return early
                     and silently skip all nine real rows.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

/* =====================================================================
   1) ProfileTypes
   ---------------------------------------------------------------------
   Every seeded row sits under the unified Visitor user type; the
   audience-vs-partner split is carried by IsForVisitor. Staff / Moderator are
   CP-only operational types and are hidden from the app sign-up picker
   (IsAppRegisterable = 0), mirroring the C# rule
   "IsAppRegisterable = MobileAppRole is not (Staff or Moderator)".

   Code is the small stable number the printed badge carries in place of the
   Guid. It is allocated max-over-every-row-plus-one — including inactive rows —
   exactly as ProfileTypeCodeAllocator does, so a retired type's code is never
   handed out twice and an existing database with admin-created types does not
   collide on the filtered unique index.
   ===================================================================== */

-- 1a) Legacy renames, ported from RenameProfileTypeIfPresentAsync. No-ops on a
--     fresh database. They run BEFORE the inserts because they exist to stop a
--     database that still carries an older name from ending up with a duplicate
--     row once the inserts below add the current one. Each is guarded against a
--     collision on the destination name, so a row an operator created by hand is
--     left alone rather than violating the unique index.
IF EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Visitor — General')
   AND NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'General')
BEGIN
    UPDATE dbo.ProfileTypes
       SET Name       = N'General',
           NameArabic = CASE WHEN NameArabic = N'زائر — عام' THEN N'عام' ELSE NameArabic END,
           UpdatedAt  = @now
     WHERE Name = N'Visitor — General';
END;

IF EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Other — Staff')
   AND NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Staff')
BEGIN
    UPDATE dbo.ProfileTypes
       SET Name       = N'Staff',
           NameArabic = CASE WHEN NameArabic = N'أخرى — فريق' THEN N'فريق' ELSE NameArabic END,
           UpdatedAt  = @now
     WHERE Name = N'Other — Staff';
END;

-- The owner fixed the visitor self-registration type's name as "Normal" (عادي);
-- rename any database still carrying the older "General" row in place.
IF EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'General')
   AND NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Normal')
BEGIN
    UPDATE dbo.ProfileTypes
       SET Name       = N'Normal',
           NameArabic = CASE WHEN NameArabic = N'عام' THEN N'عادي' ELSE NameArabic END,
           UpdatedAt  = @now
     WHERE Name = N'General';
END;

-- 1b) The eight canonical types, in the order the C# seeder created them, so a
--     fresh database allocates the same badge codes 1..8 it always did.

-- Normal (عادي) — the single audience-side type a visitor self-registers under.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Normal')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Normal', N'عادي', 1, N'#3B82F6',
        N'None', 0, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- Staff (فريق) — the canonical operational partner type: gate operations,
-- attendee lookup, badge printing.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Staff')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Staff', N'فريق', 0, N'#10B981',
        N'Staff', 0, 0, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- Moderator (منسّق) — Staff plus content/user moderation. Indigo, so it is never
-- confused with Staff green on a badge.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Moderator')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Moderator', N'منسّق', 0, N'#6366F1',
        N'Moderator', 0, 0, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- Media (إعلامي) — a display category, not operational authority.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Media')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Media', N'إعلامي', 0, N'#F59E0B',
        N'None', 0, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- Sponsor (راعي) — likewise a display category: a sponsor's representative is
-- not automatically a gate operator.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Sponsor')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Sponsor', N'راعي', 0, N'#8B5CF6',
        N'None', 0, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- Exhibitor (عارض) — unlike Media / Sponsor this one carries real app
-- authority, so the lead-capture tools (scan a visitor's QR, "My Visitors")
-- gate to it. Booth-officer accounts are assigned this type.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'Exhibitor')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'Exhibitor', N'عارض', 0, N'#0891B2',
        N'Exhibitor', 0, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- VVIP / VIP — audience tiers used by the dedicated VIP registration page and
-- the موج (Mawj) welcome-message export. Both are audience-side, both carry no
-- special app authority, and their Arabic names stay distinct so the two cards
-- never read identically in an Arabic UI.
IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'VVIP')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'VVIP', N'شخصيات بالغة الأهمية', 1, N'#B91C1C',
        N'None', 1, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.ProfileTypes WHERE Name = N'VIP')
    INSERT INTO dbo.ProfileTypes (Id, Name, NameArabic, IsForVisitor, PageColor,
        MobileAppRole, IsVipTier, IsAppRegisterable, ShowInPartnerDirectory, Code,
        CreatedAt, CreatedBy, IsActive)
    VALUES (NEWID(), N'VIP', N'كبار الشخصيات', 1, N'#0E7490',
        N'None', 1, 1, 1, (SELECT ISNULL(MAX(Code), 0) + 1 FROM dbo.ProfileTypes),
        @now, @sys, 1);

-- IsVipTier is set in the two inserts above, NOT by a follow-up UPDATE. Despite
-- the column name it has nothing to do with meetings: it decides who may
-- self-reserve a VIP-tier SEAT, and it is what the app receives as
-- UserProfileResponse.IsVip.
--
-- The C# this file replaces set the flag with an unconditional ExecuteUpdateAsync
-- on every boot, on the stated grounds that no admin path writes the column.
-- That was wrong: ProfileTypeForm renders an "IsVipTier" checkbox for exactly
-- these two rows (both are IsForVisitor), and AdminProfileTypeCommandService
-- assigns the property unconditionally so that clearing the box clears it. The
-- port of that UPDATE would therefore have silently re-ticked the flag on the
-- next seed run and put back VIP seat self-reservation an administrator had
-- deliberately taken away, with nothing logged. Seeding the value at insert
-- keeps a fresh database correct and leaves an admin edit alone.

/* =====================================================================
   2) Interests  (the visitor profile picker)
   ---------------------------------------------------------------------
   Whole-table guard: seeded only when the table is completely empty.
   ===================================================================== */

IF NOT EXISTS (SELECT 1 FROM dbo.Interests)
BEGIN
    INSERT INTO dbo.Interests (Id, Name, NameArabic, DisplayOrder, CreatedAt, CreatedBy, IsActive)
    VALUES
        (NEWID(), N'Naval Defence Technologies',          N'تقنيات الدفاع البحري',              1,  @now, @sys, 1),
        (NEWID(), N'Maritime Security',                   N'الأمن البحري',                      2,  @now, @sys, 1),
        (NEWID(), N'Shipbuilding & Marine Industries',    N'بناء السفن والصناعات البحرية',      3,  @now, @sys, 1),
        (NEWID(), N'Ports & Maritime Logistics',          N'الموانئ والخدمات اللوجستية البحرية', 4,  @now, @sys, 1),
        (NEWID(), N'Hydrography & Marine Survey',         N'الهيدروغرافيا والمسح البحري',       5,  @now, @sys, 1),
        (NEWID(), N'Marine Environment & Sustainability', N'البيئة البحرية والاستدامة',         6,  @now, @sys, 1),
        (NEWID(), N'Autonomous & Unmanned Systems',       N'الأنظمة ذاتية التشغيل وغير المأهولة', 7,  @now, @sys, 1),
        (NEWID(), N'Maritime Cybersecurity',              N'الأمن السيبراني البحري',            8,  @now, @sys, 1),
        (NEWID(), N'Investment & Local Content',          N'الاستثمار والمحتوى المحلي',         9,  @now, @sys, 1),
        (NEWID(), N'Research & Innovation',               N'البحث والابتكار',                   10, @now, @sys, 1);
END;

/* =====================================================================
   3) Organisations  (the profile's required الجهة pick)
   ---------------------------------------------------------------------
   Guarded on "no organisation other than the seeded catch-all exists". The
   catch-all (Organisation.OtherId) ships with the EF model, so a fresh database
   already has one row and a plain emptiness test would skip everything below.
   ===================================================================== */

IF NOT EXISTS (SELECT 1 FROM dbo.Organisations
               WHERE Id <> 'A17E9C42-0B6D-4F58-9E31-7C2A8D5F60B4')
BEGIN
    INSERT INTO dbo.Organisations (Id, Name, NameArabic, Sector, CreatedAt, CreatedBy, IsActive)
    VALUES
        (NEWID(), N'Royal Saudi Naval Forces',                            N'القوات البحرية الملكية السعودية',        N'Government',          @now, @sys, 1),
        (NEWID(), N'Ministry of Defense',                                 N'وزارة الدفاع',                           N'Government',          @now, @sys, 1),
        (NEWID(), N'Saudi Ports Authority (Mawani)',                      N'الهيئة العامة للموانئ (موانئ)',          N'Government',          @now, @sys, 1),
        (NEWID(), N'Saudi Arabian Military Industries (SAMI)',            N'الشركة السعودية للصناعات العسكرية',      N'Defence',             @now, @sys, 1),
        (NEWID(), N'Bahri',                                               N'الشركة الوطنية السعودية للنقل البحري (البحري)', N'Shipping & Logistics', @now, @sys, 1),
        (NEWID(), N'Saudi Aramco',                                        N'أرامكو السعودية',                        N'Energy',              @now, @sys, 1),
        (NEWID(), N'Zamil Offshore',                                      N'شركة الزامل أوفشور',                     N'Marine Services',     @now, @sys, 1),
        (NEWID(), N'King Fahd University of Petroleum and Minerals',      N'جامعة الملك فهد للبترول والمعادن',       N'Academia',            @now, @sys, 1),
        (NEWID(), N'King Abdullah University of Science and Technology (KAUST)', N'جامعة الملك عبدالله للعلوم والتقنية', N'Academia',        @now, @sys, 1);
END;

COMMIT TRANSACTION;
