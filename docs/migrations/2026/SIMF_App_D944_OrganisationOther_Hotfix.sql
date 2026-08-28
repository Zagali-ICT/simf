/*
  SIMF_App — interim hotfix for D-944 on a RUNNING production database:
  add dbo.UserProfiles.OrganisationOther and seed the "Other" organisation.

  WHY THIS FILE EXISTS — READ THIS BEFORE THE NEXT SCHEMA CHANGE
    The App schema is described by exactly ONE migration per context
    (`00000000000000_InitialCreate`, D-110 / D-895), and a schema change is a
    REGENERATION of that migration rather than a new one. That is the right
    design for the freeze, and it has a consequence nothing in the repository
    stated until now:

        A REGENERATED InitialCreate IS A NO-OP AGAINST A DATABASE THAT
        ALREADY HAS ONE.

    Production's __EFMigrationsHistory already records
    `00000000000000_InitialCreate` as applied, so `MigrateAsync` at API startup
    finds nothing pending and applies nothing. The regenerated file describes a
    newer schema that the live database never receives. D-881 hid this by
    dropping both databases when it regenerated them ("no data worth
    preserving"); with real data that is not an option.

  WHAT BROKE, 2026-08-28
    The API deployed cleanly and the code-only half of the change worked -
    `GET /app/account/user-profile/countries` returned the new `phonePrefix` on
    all 59 rows. The schema half did not exist, so:

      * `GET /app/account/user-profile` -> HTTP 500. EF selects
        `OrganisationOther`; SQL Server answers Invalid column name. EVERY
        visitor's profile read, on the screen that gates registration.
      * `GET /app/organisations?search=<no match>` -> `[]` instead of the
        catch-all row, because the seeded Organisation was in the migration's
        InsertData and the migration never ran.

  THE PERMANENT FIX (not this script)
    A FRESH database (drop + MigrateAsync) creates the column and inserts the
    seed row from the regenerated migration with no help from this file. This
    script exists only for a live database that must keep its data.

  SAFE TO RE-RUN. Both statements are guarded; running it twice changes
  nothing. It takes no locks worth naming - the ALTER adds a NULLable column,
  which is a metadata-only operation in SQL Server.

  RUN AGAINST: SIMF_App  (NOT SIMF_Identity)
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @OtherId uniqueidentifier = 'A17E9C42-0B6D-4F58-9E31-7C2A8D5F60B4';

/* ---------------------------------------------------------------------
   1) The column. nvarchar(150) NULL — matches Organisation.NameArabic's own
   ceiling, so a name later promoted into the lookup cannot be truncated on
   the way in. Nullable because it is set only alongside the catch-all pick.
   --------------------------------------------------------------------- */
IF COL_LENGTH('dbo.UserProfiles', 'OrganisationOther') IS NULL
BEGIN
    ALTER TABLE dbo.UserProfiles ADD OrganisationOther nvarchar(150) NULL;
    PRINT 'Added dbo.UserProfiles.OrganisationOther.';
END
ELSE
    PRINT 'dbo.UserProfiles.OrganisationOther already present - skipped.';

/* ---------------------------------------------------------------------
   2) The catch-all organisation, matching the migration's InsertData exactly
   (same fixed id, same values). Organisation is a REQUIRED field on the
   sign-up form (D-221) and the list is a curated government import, so
   without this row a visitor whose employer is absent cannot register at all.

   CommercialRegistration stays NULL deliberately: the government Excel import
   matches on that column, so a re-import can never update or duplicate this
   row. CreatedBy is Guid.Empty, as the migration writes it — this row has no
   human author.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Organisations WHERE Id = @OtherId)
BEGIN
    INSERT INTO dbo.Organisations
        (Id, City, CommercialRegistration, CreatedAt, CreatedBy, DeletedAt,
         Email, IsActive, Name, NameArabic, Phone, Sector, UpdatedAt,
         UpdatedBy, Website)
    VALUES
        (@OtherId, NULL, NULL, '2026-01-01T00:00:00',
         '00000000-0000-0000-0000-000000000000', NULL,
         NULL, 1, N'Other', N'أخرى', NULL, NULL, NULL, NULL, NULL);
    PRINT 'Seeded the "Other" organisation.';
END
ELSE
    PRINT 'The "Other" organisation already exists - skipped.';

/* ---------------------------------------------------------------------
   3) Prove it, so a silent partial apply cannot pass for success.
   --------------------------------------------------------------------- */
IF COL_LENGTH('dbo.UserProfiles', 'OrganisationOther') IS NULL
    THROW 51000, 'OrganisationOther is still missing - the ALTER did not apply.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Organisations WHERE Id = @OtherId AND IsActive = 1)
    THROW 51001, 'The "Other" organisation is missing or inactive.', 1;

PRINT 'D-944 hotfix verified: column present, catch-all row active.';
