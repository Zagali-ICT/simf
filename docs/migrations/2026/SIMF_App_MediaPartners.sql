/* =====================================================================
   SIMF_App — media-partners seed  ->  GET /app/media-partners

   Target database : SIMF_App   (NOT SIMF_Identity)
   Safe to re-run  : YES — every insert is guarded by IF NOT EXISTS.
   Transactional   : one transaction with SET XACT_ABORT ON.

   Provenance      : ported verbatim from IdentitySeeder.EnsureDemoPartners
                     (D-348); the content-seeding lane moved from C# to this
                     SQL file (D-718 / owner rule — see README).

   >>> PLACEHOLDER content — three demo media partners so the partners strip
       is not empty. Replace the names and upload real logos through the
       Control Panel for the client's real 2026 media partners.

   >>> The placeholder logos are seeded the way every other file in this system
       is: as rows in dbo.StoredFiles, owned by the partner, resolved by the
       asset pipeline. They use SourceType = 1 (ExternalLink), which stores no
       bytes and serves by redirect — the shape the store already provides for
       a file somebody else hosts.

       They used to be raw placehold.co URLs written straight into
       MediaPartners.LogoRelativePath, and this was the only seed in the tree
       that put a URL in an entity column. It blocked that column from becoming
       a typed key, and it meant the demo logos carried none of what a
       StoredFile row carries: media type, service policy, sensitivity tier,
       retention, or an owner. This is the Wave C P6/E2 follow-up the File Store
       Dev Guide has listed as outstanding since 2026-07-07.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor (matches the app seeders)

/* The StoredFiles values used below, so the numbers are not magic:
     Service          = 9  FileService.MediaPartnerLogo
     SensitivityTier  = 0  Public
     FileType         = 0  Image
     SourceType       = 1  ExternalLink — AssetService.ResolveAsync returns the
                           URL rather than reading bytes
     OwnerEntityType  = 8  MediaPartner
   StorageKey, SizeBytes and Sha256 stay NULL: there are no bytes to describe.
   The StoredFile ids are fixed rather than NEWID() so a re-run is a no-op. */

-- ---------------------------------------------------------------- partner 1
IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Maritime News Network')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Maritime News Network', N'شبكة الأخبار البحرية', 10,
        NULL, 1, @now, @sys);

IF EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Maritime News Network')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 9 AND IsActive = 1
                     AND OwnerEntityId = (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Maritime News Network'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, ExternalUrl, ContentType,
        IsDeletable, OwnerEntityType, OwnerEntityId, CreatedAt, CreatedBy, IsActive)
    VALUES ('b1a7f0d2-1c4e-4a11-9f01-0a1d2e3f4a51', 9, 0, 0, 1,
        0, 0, N'https://placehold.co/260x130/ffffff/0a2e6b?text=Maritime%20News%20Network', N'image/png',
        1, 8, (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Maritime News Network'),
        @now, @sys, 1);

-- ---------------------------------------------------------------- partner 2
IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Naval Affairs Review')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Naval Affairs Review', N'مجلة الشؤون البحرية', 20,
        NULL, 1, @now, @sys);

IF EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Naval Affairs Review')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 9 AND IsActive = 1
                     AND OwnerEntityId = (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Naval Affairs Review'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, ExternalUrl, ContentType,
        IsDeletable, OwnerEntityType, OwnerEntityId, CreatedAt, CreatedBy, IsActive)
    VALUES ('b1a7f0d2-1c4e-4a11-9f01-0a1d2e3f4a52', 9, 0, 0, 1,
        0, 0, N'https://placehold.co/260x130/ffffff/0a2e6b?text=Naval%20Affairs%20Review', N'image/png',
        1, 8, (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Naval Affairs Review'),
        @now, @sys, 1);

-- ---------------------------------------------------------------- partner 3
IF NOT EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Sea Trade Daily')
    INSERT INTO dbo.MediaPartners (Id, Name, NameArabic, DisplayOrder, LogoRelativePath, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), N'Sea Trade Daily', N'تجارة البحار اليومية', 30,
        NULL, 1, @now, @sys);

IF EXISTS (SELECT 1 FROM dbo.MediaPartners WHERE Name = N'Sea Trade Daily')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 9 AND IsActive = 1
                     AND OwnerEntityId = (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Sea Trade Daily'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, ExternalUrl, ContentType,
        IsDeletable, OwnerEntityType, OwnerEntityId, CreatedAt, CreatedBy, IsActive)
    VALUES ('b1a7f0d2-1c4e-4a11-9f01-0a1d2e3f4a53', 9, 0, 0, 1,
        0, 0, N'https://placehold.co/260x130/ffffff/0a2e6b?text=Sea%20Trade%20Daily', N'image/png',
        1, 8, (SELECT Id FROM dbo.MediaPartners WHERE Name = N'Sea Trade Daily'),
        @now, @sys, 1);

COMMIT TRANSACTION;
