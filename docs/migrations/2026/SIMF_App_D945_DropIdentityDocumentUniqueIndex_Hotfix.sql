/*
  SIMF_App - D-945 delta for a RUNNING production database:
  drop the cross-profile duplicate-identity unique index.

  WHY THIS FILE EXISTS
    The App schema is described by exactly ONE migration per context
    (00000000000000_InitialCreate, D-110 / D-895), and a schema change is a
    REGENERATION of that migration rather than a new one. A REGENERATED
    InitialCreate IS A NO-OP AGAINST A DATABASE THAT ALREADY HAS ONE:
    __EFMigrationsHistory already records it as applied, so MigrateAsync finds
    nothing pending and the live database never receives the change.

    D-944 shipped without its delta on 2026-08-28 and answered HTTP 500 on
    every visitor profile read until a hotfix ran. This file is that lesson
    applied: the code half of D-945 is harmless without it (the guard is simply
    gone), but the INDEX would survive on prod and keep rejecting the very
    registrations the change exists to allow.

  WHAT IT DOES
    Drops IX_ProfileIdentityDocuments_NumberHash. That index made a national ID
    / Iqama / passport number unique ACROSS profiles, so a visitor whose number
    already sat on any earlier profile could not register at all and the desk
    had no way to release it. Removed on owner instruction, 2026-08-29.

  WHAT IT DELIBERATELY DOES NOT DO
    It does NOT touch IX_ProfileIdentityDocuments_ProfileId_Kind, the OTHER
    unique index on the same table. That one bounds a SINGLE profile to one
    national ID, one Iqama and one passport. It was never the registration
    blocker, and dropping it would let one profile hold two passports with the
    read path forced to pick one.

    The NumberHash COLUMN is kept. Nothing reads it now, but ProfileIdentityDocument.Number
    is AES-GCM encrypted under a random nonce and can never be equality-queried,
    so the digest is the only seam any future document-number lookup could use.
    Dropping it is a separate decision.

  SAFE TO RE-RUN. Guarded; running it twice changes nothing.

  RUN AGAINST: SIMF_App  (NOT SIMF_Identity)
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_ProfileIdentityDocuments_NumberHash'
             AND object_id = OBJECT_ID('dbo.ProfileIdentityDocuments'))
BEGIN
    DROP INDEX [IX_ProfileIdentityDocuments_NumberHash] ON dbo.ProfileIdentityDocuments;
    PRINT 'Dropped IX_ProfileIdentityDocuments_NumberHash.';
END
ELSE
    PRINT 'IX_ProfileIdentityDocuments_NumberHash already absent - skipped.';

/* Prove it, so a silent partial apply cannot pass for success. */
IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE name = 'IX_ProfileIdentityDocuments_NumberHash'
             AND object_id = OBJECT_ID('dbo.ProfileIdentityDocuments'))
    THROW 51000, 'The duplicate-identity index is still present - the DROP did not apply.', 1;

/* And prove the one that must SURVIVE is still there. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_ProfileIdentityDocuments_ProfileId_Kind'
                 AND object_id = OBJECT_ID('dbo.ProfileIdentityDocuments'))
    THROW 51001, 'IX_ProfileIdentityDocuments_ProfileId_Kind is missing - it must NOT be dropped.', 1;

PRINT 'D-945 verified: cross-profile duplicate index gone, per-profile kind index intact.';
