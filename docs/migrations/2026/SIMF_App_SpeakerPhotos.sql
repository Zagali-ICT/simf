/* =====================================================================
   SIMF_App — SPEAKER PHOTOS (centralized StoredFile store)

   Companion to SIMF_App_Speakers.sql (same folder). Seeds one StoredFile row per
   speaker that has a real headshot in the SIMF-4 deck (23 of 32; the other 9
   deck slides carry no photo). SpeakerPhoto is a PUBLIC, plaintext (un-encrypted)
   file service, so the bytes can be pre-placed on disk and served as-is.

   TWO STEPS — both required:
     1) RUN this script against SIMF_App  (AFTER SIMF_App_Speakers.sql, so the
        speakers it points at already exist). Idempotent (IF NOT EXISTS).
     2) DEPLOY the image folder: copy
            docs/migrations/2026/speaker-photos/speakerphoto
        into the API file-storage root  <FileStorage:RootPath>. In PRODUCTION
        that is pinned to  C:\SIMF\Storage\files  (deploy/set-env-api.ps1, D-718)
        -> giving  C:\SIMF\Storage\files\speakerphoto\{id}.{ext}. (Dev default is
        App_Data/files.) The root is OUTSIDE the IIS site on purpose so the
        `robocopy /MIR` deploy never purges it. The StorageKey below is
        speakerphoto/{StoredFile.Id:N}.{ext}, resolved under that root by
        FilesystemFileStorageProvider.

   Serving: the app fetches a speaker photo by speaker id; AssetService resolves
   the StoredFile by (Service=SpeakerPhoto, OwnerEntityId=<speaker id>, IsActive)
   and streams the plaintext bytes. If the row exists but the file was not
   deployed, the serve simply 404s — re-deploy the folder to fix.

   Enum ints: Service SpeakerPhoto=4, SensitivityTier Public=0, FileType Image=0,
   SourceType Upload=0, OwnerEntityType Speaker=2.  Safe to re-run.
   ===================================================================== */

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DECLARE @now datetimeoffset   = SYSDATETIMEOFFSET();
DECLARE @sys uniqueidentifier = '00000000-0000-0000-0000-000000000000'; -- system/seeder actor

-- SIMF4-SPK-01  ->  speakerphoto/0f837c7c4f1655f1b930dcd924eecb89.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-01')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-01'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('0f837c7c-4f16-55f1-b930-dcd924eecb89', 4, 0, 0, 0,
        0, 0, N'speakerphoto/0f837c7c4f1655f1b930dcd924eecb89.png', N'SIMF4-SPK-01.png', N'image/png',
        113659, N'0b47ff330c3476db6a7a0e234e971e048365b0ac5141f77f7f19af0592d67efc', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-01'),
        @now, @sys, 1);
-- SIMF4-SPK-02  ->  speakerphoto/7296dd95227f52de89293bb551c4fd77.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-02')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-02'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('7296dd95-227f-52de-8929-3bb551c4fd77', 4, 0, 0, 0,
        0, 0, N'speakerphoto/7296dd95227f52de89293bb551c4fd77.png', N'SIMF4-SPK-02.png', N'image/png',
        91046, N'9f24092b376e13d2dba233f200a4d7bc00868cf67a74b11fd4aafae3f42386ce', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-02'),
        @now, @sys, 1);
-- SIMF4-SPK-03  ->  speakerphoto/6d81f2753f4954f982e89f22413f7552.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-03')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-03'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('6d81f275-3f49-54f9-82e8-9f22413f7552', 4, 0, 0, 0,
        0, 0, N'speakerphoto/6d81f2753f4954f982e89f22413f7552.png', N'SIMF4-SPK-03.png', N'image/png',
        106190, N'c6d9242bf9b5929b23f2c8d9746bbb22b9a4c20575097a48e7b938653380d9ca', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-03'),
        @now, @sys, 1);
-- SIMF4-SPK-04  ->  speakerphoto/a6bcb906acb25d969af03a3cae22301b.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-04')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-04'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('a6bcb906-acb2-5d96-9af0-3a3cae22301b', 4, 0, 0, 0,
        0, 0, N'speakerphoto/a6bcb906acb25d969af03a3cae22301b.png', N'SIMF4-SPK-04.png', N'image/png',
        189264, N'7f7d5a46e5c2c16321511a4df1f7dda262bfa333ca593a3971e35bb2165d938e', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-04'),
        @now, @sys, 1);
-- SIMF4-SPK-05  ->  speakerphoto/ad30f57df3e556e69c332a3c73a81b78.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-05')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-05'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('ad30f57d-f3e5-56e6-9c33-2a3c73a81b78', 4, 0, 0, 0,
        0, 0, N'speakerphoto/ad30f57df3e556e69c332a3c73a81b78.png', N'SIMF4-SPK-05.png', N'image/png',
        46518, N'cef10a32c84f1b2256b3ed89586df687d0852d18f297ac928bae0b247fbf99a3', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-05'),
        @now, @sys, 1);
-- SIMF4-SPK-06  ->  speakerphoto/b069ba68a6d8568aa3aec66d8614fc09.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-06')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-06'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('b069ba68-a6d8-568a-a3ae-c66d8614fc09', 4, 0, 0, 0,
        0, 0, N'speakerphoto/b069ba68a6d8568aa3aec66d8614fc09.png', N'SIMF4-SPK-06.png', N'image/png',
        51050, N'8b54e40fb61bceb2fccb01cde938162e551e543c6426ba65cde4b28617fb9c78', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-06'),
        @now, @sys, 1);
-- SIMF4-SPK-07  ->  speakerphoto/60469802bd9c5cca985b7018ae20b885.jpeg
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-07')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-07'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('60469802-bd9c-5cca-985b-7018ae20b885', 4, 0, 0, 0,
        0, 0, N'speakerphoto/60469802bd9c5cca985b7018ae20b885.jpeg', N'SIMF4-SPK-07.jpeg', N'image/jpeg',
        47223, N'1a95044d4e52c4e64dc64887414848f2c3014238728ab5638111422fd2d96dcf', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-07'),
        @now, @sys, 1);
-- SIMF4-SPK-08  ->  speakerphoto/96197390a43c59b5aa83bc7e7fe845f0.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-08')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-08'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('96197390-a43c-59b5-aa83-bc7e7fe845f0', 4, 0, 0, 0,
        0, 0, N'speakerphoto/96197390a43c59b5aa83bc7e7fe845f0.png', N'SIMF4-SPK-08.png', N'image/png',
        145844, N'2b1d39c8a81274b58dadcb7e7e9eb93845ee198823ee4200d607b55c9c8c2b5e', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-08'),
        @now, @sys, 1);
-- SIMF4-SPK-10  ->  speakerphoto/004472722b045b4ea633b6b5ba286555.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-10')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-10'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('00447272-2b04-5b4e-a633-b6b5ba286555', 4, 0, 0, 0,
        0, 0, N'speakerphoto/004472722b045b4ea633b6b5ba286555.png', N'SIMF4-SPK-10.png', N'image/png',
        160568, N'0e1d1595e33ab9cca47855d7df5ba7de0c020dab45846a7040bcbba939ba5568', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-10'),
        @now, @sys, 1);
-- SIMF4-SPK-12  ->  speakerphoto/2e15d328c30d554cabd1f87c486b38a2.jpeg
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-12')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-12'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('2e15d328-c30d-554c-abd1-f87c486b38a2', 4, 0, 0, 0,
        0, 0, N'speakerphoto/2e15d328c30d554cabd1f87c486b38a2.jpeg', N'SIMF4-SPK-12.jpeg', N'image/jpeg',
        80853, N'547e0751ec9d452424c87d1b712b8629e12044a5ddd67bbbae45c6921699a251', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-12'),
        @now, @sys, 1);
-- SIMF4-SPK-13  ->  speakerphoto/6ab0d8a54e3f507f8d5ccb0778759386.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-13')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-13'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('6ab0d8a5-4e3f-507f-8d5c-cb0778759386', 4, 0, 0, 0,
        0, 0, N'speakerphoto/6ab0d8a54e3f507f8d5ccb0778759386.png', N'SIMF4-SPK-13.png', N'image/png',
        245296, N'b06a63fe9e80a2ae3beb50d585114e366f0e2b47cb53edfa3d1b715b803927d0', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-13'),
        @now, @sys, 1);
-- SIMF4-SPK-14  ->  speakerphoto/442d3535cbb3590cb1edb4584eef4dc4.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-14')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-14'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('442d3535-cbb3-590c-b1ed-b4584eef4dc4', 4, 0, 0, 0,
        0, 0, N'speakerphoto/442d3535cbb3590cb1edb4584eef4dc4.png', N'SIMF4-SPK-14.png', N'image/png',
        71112, N'8059c9b8b51c513c6821168415e73a1716d9554b970cbe0276c8b462b95eb571', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-14'),
        @now, @sys, 1);
-- SIMF4-SPK-15  ->  speakerphoto/74970c0328f55698b20f7dcc2fc6ce88.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-15')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-15'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('74970c03-28f5-5698-b20f-7dcc2fc6ce88', 4, 0, 0, 0,
        0, 0, N'speakerphoto/74970c0328f55698b20f7dcc2fc6ce88.png', N'SIMF4-SPK-15.png', N'image/png',
        133869, N'dec7bd76ad97ee5f3b598c770924ac58d83d577694afd9edcb4afcf4483b2f17', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-15'),
        @now, @sys, 1);
-- SIMF4-SPK-17  ->  speakerphoto/670cb782288653dc829646c4549ff62f.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-17')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-17'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('670cb782-2886-53dc-8296-46c4549ff62f', 4, 0, 0, 0,
        0, 0, N'speakerphoto/670cb782288653dc829646c4549ff62f.png', N'SIMF4-SPK-17.png', N'image/png',
        237795, N'7006c4e7d0c87896dcf689c366283984b0b9d9b4345d5df8a98019bb60150381', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-17'),
        @now, @sys, 1);
-- SIMF4-SPK-18  ->  speakerphoto/28d8e65b094f521a80c1bb7ddd5b644a.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-18')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-18'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('28d8e65b-094f-521a-80c1-bb7ddd5b644a', 4, 0, 0, 0,
        0, 0, N'speakerphoto/28d8e65b094f521a80c1bb7ddd5b644a.png', N'SIMF4-SPK-18.png', N'image/png',
        66219, N'a1b485b864f079fc84af8c03c7285eda1a06c1946d03f5223c960922cf0a9884', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-18'),
        @now, @sys, 1);
-- SIMF4-SPK-20  ->  speakerphoto/462648c9cf995527aedfac01dff99f73.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-20')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-20'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('462648c9-cf99-5527-aedf-ac01dff99f73', 4, 0, 0, 0,
        0, 0, N'speakerphoto/462648c9cf995527aedfac01dff99f73.png', N'SIMF4-SPK-20.png', N'image/png',
        116325, N'f0739f8579b4d9458d09f30c608fee07cb04f7a196f4a612433dc078d1593bb2', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-20'),
        @now, @sys, 1);
-- SIMF4-SPK-23  ->  speakerphoto/08b23213a71351708eeb2e4af46150ca.jpg
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-23')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-23'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('08b23213-a713-5170-8eeb-2e4af46150ca', 4, 0, 0, 0,
        0, 0, N'speakerphoto/08b23213a71351708eeb2e4af46150ca.jpg', N'SIMF4-SPK-23.jpg', N'image/jpeg',
        46884, N'ea66ff6adc98adb8594e3f577247264093795471557e5155b7fe78e5d5f35662', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-23'),
        @now, @sys, 1);
-- SIMF4-SPK-27  ->  speakerphoto/95ff57c797e25ee3a66af09bfb050ed9.jpg
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-27')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-27'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('95ff57c7-97e2-5ee3-a66a-f09bfb050ed9', 4, 0, 0, 0,
        0, 0, N'speakerphoto/95ff57c797e25ee3a66af09bfb050ed9.jpg', N'SIMF4-SPK-27.jpg', N'image/jpeg',
        78423, N'7da1ec8ee4fb2b66c09944670321b6ae0a8e3b879a1f4883283a9bddb14f1f4c', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-27'),
        @now, @sys, 1);
-- SIMF4-SPK-28  ->  speakerphoto/8eaed19817635e968a33c37b3064db04.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-28')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-28'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('8eaed198-1763-5e96-8a33-c37b3064db04', 4, 0, 0, 0,
        0, 0, N'speakerphoto/8eaed19817635e968a33c37b3064db04.png', N'SIMF4-SPK-28.png', N'image/png',
        103818, N'6a8c812324d0440fd1bc1cf18aa56e24812a3f2076e175899b4e71670f590c5c', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-28'),
        @now, @sys, 1);
-- SIMF4-SPK-29  ->  speakerphoto/5e007954d7bb54c29dfafb840d7d965f.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-29')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-29'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('5e007954-d7bb-54c2-9dfa-fb840d7d965f', 4, 0, 0, 0,
        0, 0, N'speakerphoto/5e007954d7bb54c29dfafb840d7d965f.png', N'SIMF4-SPK-29.png', N'image/png',
        91380, N'054170de067242e2b1601d17806d0b1e120892e0118a91011c9f861402471ad2', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-29'),
        @now, @sys, 1);
-- SIMF4-SPK-30  ->  speakerphoto/e0cd3802f9ca53e69d8e8806632ff043.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-30')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-30'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('e0cd3802-f9ca-53e6-9d8e-8806632ff043', 4, 0, 0, 0,
        0, 0, N'speakerphoto/e0cd3802f9ca53e69d8e8806632ff043.png', N'SIMF4-SPK-30.png', N'image/png',
        261059, N'590a45382c88566a68d0fa5b15bc2d50c924e132cfe978edb09a55a3017162da', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-30'),
        @now, @sys, 1);
-- SIMF4-SPK-31  ->  speakerphoto/60e6b9ad90de5b1a8d559de6ae48bb4b.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-31')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-31'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('60e6b9ad-90de-5b1a-8d55-9de6ae48bb4b', 4, 0, 0, 0,
        0, 0, N'speakerphoto/60e6b9ad90de5b1a8d559de6ae48bb4b.png', N'SIMF4-SPK-31.png', N'image/png',
        206632, N'3e19c37ccd42f1acf791df513f55194a0fbef9359db8196f1dc03cfe773a052d', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-31'),
        @now, @sys, 1);
-- SIMF4-SPK-32  ->  speakerphoto/88cd8505f25f5e94bd83d316c72ef0ed.png
IF EXISTS (SELECT 1 FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-32')
   AND NOT EXISTS (SELECT 1 FROM dbo.StoredFiles
                   WHERE Service = 4 AND IsActive = 1 AND OwnerEntityId = (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-32'))
    INSERT INTO dbo.StoredFiles (Id, Service, SensitivityTier, FileType, SourceType,
        IsEncrypted, CipherFormatVersion, StorageKey, OriginalFileName, ContentType,
        SizeBytes, Sha256, IsDeletable, OwnerEntityType, OwnerEntityId,
        CreatedAt, CreatedBy, IsActive)
    VALUES ('88cd8505-f25f-5e94-bd83-d316c72ef0ed', 4, 0, 0, 0,
        0, 0, N'speakerphoto/88cd8505f25f5e94bd83d316c72ef0ed.png', N'SIMF4-SPK-32.png', N'image/png',
        483216, N'7f9967019affdf3df70d745de7820be6c8aae50054ef7be1209817f300369360', 1, 2, (SELECT Id FROM dbo.Speakers WHERE Code = N'SIMF4-SPK-32'),
        @now, @sys, 1);

COMMIT TRANSACTION;

/* Verification — expect 23 active SpeakerPhoto rows owned by SIMF-4 speakers. */
SELECT COUNT(*) AS Simf4SpeakerPhotos
FROM dbo.StoredFiles sf
JOIN dbo.Speakers s ON s.Id = sf.OwnerEntityId
WHERE sf.Service = 4 AND sf.IsActive = 1 AND s.Code LIKE 'SIMF4-SPK-%';
