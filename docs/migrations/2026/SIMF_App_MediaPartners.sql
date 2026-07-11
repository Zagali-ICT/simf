/* =====================================================================
   SIMF_App — media-partners seed  ->  GET /app/media-partners

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded by IF NOT EXISTS (Name).
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder.EnsureDemoPartners
                     (D-348); the content-seeding lane moved from C# to this
                     SQL file (D-718 / owner rule — see README).

   >>> PLACEHOLDER content — three demo media partners so the partners strip
       is not empty. LogoRelativePath points at an external placeholder image
       (placehold.co); replace the names + upload real logos (StoredFile) via
       the Control Panel for the client's real 2026 media partners.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Maritime News Network')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Maritime News Network', N'شبكة الأخبار البحرية', 10,
        N'https://placehold.co/260x130/ffffff/0a2e6b?text=Maritime%20News%20Network', 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Naval Affairs Review')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Naval Affairs Review', N'مجلة الشؤون البحرية', 20,
        N'https://placehold.co/260x130/ffffff/0a2e6b?text=Naval%20Affairs%20Review', 1, @now, @sys);

IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Sea Trade Daily')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Sea Trade Daily', N'تجارة البحار اليومية', 30,
        N'https://placehold.co/260x130/ffffff/0a2e6b?text=Sea%20Trade%20Daily', 1, @now, @sys);

COMMIT TRANSACTION;
