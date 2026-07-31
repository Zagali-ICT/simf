/*
  Structural fingerprint of a SIMF database: every column (name, type, precision,
  nullability), every index (uniqueness, key order, includes, filter), every CHECK
  constraint and every foreign key, one object per line, sorted.

  WHY IT EXISTS
    To answer "does this database really match the EF model?" with evidence rather
    than trust. Its first use was the 2026-07-31 UTC -> Saudi-local cutover
    (`docs/migrations/2026/SIMF_UtcToSaudiLocal_DateTime.sql`): that script rewrites
    the migration history to say the consolidated baseline is applied, so if the
    schema did NOT match, EF would never notice and the mismatch would surface later
    as a runtime error on some rarely-hit table. Diffing against a database built
    fresh from the migrations catches it immediately.

    It found two genuine defects on first run: `AspNetUsers.LockoutEnd` being
    converted when the model still maps it `datetimeoffset`, and six schema additions
    the pre-cutover database was missing entirely.

  HOW TO USE IT
    1. Build a scratch database straight from the migrations:

         $env:SIMF_DESIGN_TIME_APP_CONNECTION      = "Server=.;Database=SIMF_App_Fresh;Trusted_Connection=True;TrustServerCertificate=True"
         $env:SIMF_DESIGN_TIME_IDENTITY_CONNECTION = "Server=.;Database=SIMF_Identity_Fresh;Trusted_Connection=True;TrustServerCertificate=True"
         dotnet ef database update --project src\Backend\SIMF.Infrastructure --startup-project src\Backend\SIMF.Api --context SimfAppDbContext
         dotnet ef database update --project src\Backend\SIMF.Infrastructure --startup-project src\Backend\SIMF.Api --context SimfIdentityDbContext

    2. Fingerprint both and diff:

         sqlcmd -S . -E -d SIMF_App       -i tools\qa\schema-fingerprint.sql -h -1 -Y 500 -o real.txt
         sqlcmd -S . -E -d SIMF_App_Fresh -i tools\qa\schema-fingerprint.sql -h -1 -Y 500 -o fresh.txt
         Compare-Object (gc real.txt) (gc fresh.txt)

       No output means the schemas are identical. Repeat for the Identity pair.

    3. Drop the scratch databases when done.

  NOTE ON COLLATION
    Catalog name columns are sysname (Latin1_General_CI_AS_KS_WS) while
    `cc.definition` / `filter_definition` carry the database collation, and
    concatenating two differently-collated implicit operands is a hard error — hence
    COLLATE DATABASE_DEFAULT on every name.

  The migration-history tables are excluded: their contents are deployment state, not
  schema, and they legitimately differ between a migrated and a fresh database.
*/
SET NOCOUNT ON;

SELECT 'COL|' + t.name COLLATE DATABASE_DEFAULT
       + '|' + c.name COLLATE DATABASE_DEFAULT
       + '|' + TYPE_NAME(c.system_type_id) COLLATE DATABASE_DEFAULT
       + '|len=' + CONVERT(varchar, c.max_length)
       + '|prec=' + CONVERT(varchar, c.precision)
       + '|scale=' + CONVERT(varchar, c.scale)
       + '|null=' + CONVERT(varchar, c.is_nullable) AS Line
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.is_ms_shipped = 0 AND t.name NOT LIKE 'plan_persist%' AND t.name NOT LIKE '\_\_EF%' ESCAPE '\'

UNION ALL
SELECT 'IDX|' + t.name COLLATE DATABASE_DEFAULT
       + '|' + i.name COLLATE DATABASE_DEFAULT
       + '|uniq=' + CONVERT(varchar, i.is_unique)
       + '|pk=' + CONVERT(varchar, i.is_primary_key)
       + '|keys=' + ISNULL((SELECT STRING_AGG(CONCAT(kc.name COLLATE DATABASE_DEFAULT,
                                   CASE WHEN kic.is_descending_key = 1 THEN ':D' ELSE ':A' END), ',')
                            WITHIN GROUP (ORDER BY kic.key_ordinal)
                            FROM sys.index_columns kic
                            JOIN sys.columns kc ON kc.object_id = kic.object_id AND kc.column_id = kic.column_id
                            WHERE kic.object_id = i.object_id AND kic.index_id = i.index_id
                              AND kic.is_included_column = 0), '-')
       + '|inc=' + ISNULL((SELECT STRING_AGG(nc.name COLLATE DATABASE_DEFAULT, ',')
                            WITHIN GROUP (ORDER BY nic.index_column_id)
                           FROM sys.index_columns nic
                           JOIN sys.columns nc ON nc.object_id = nic.object_id AND nc.column_id = nic.column_id
                           WHERE nic.object_id = i.object_id AND nic.index_id = i.index_id
                             AND nic.is_included_column = 1), '-')
       + '|filt=' + ISNULL(i.filter_definition COLLATE DATABASE_DEFAULT, '-')
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.type IN (1, 2) AND t.is_ms_shipped = 0
  AND t.name NOT LIKE 'plan_persist%' AND t.name NOT LIKE '\_\_EF%' ESCAPE '\'

UNION ALL
SELECT 'CHK|' + OBJECT_NAME(cc.parent_object_id) COLLATE DATABASE_DEFAULT
       + '|' + cc.name COLLATE DATABASE_DEFAULT
       + '|' + cc.definition COLLATE DATABASE_DEFAULT
       + '|trusted=' + CONVERT(varchar, CASE WHEN cc.is_not_trusted = 1 THEN 0 ELSE 1 END)
FROM sys.check_constraints cc
JOIN sys.tables t ON t.object_id = cc.parent_object_id
WHERE t.is_ms_shipped = 0 AND cc.is_ms_shipped = 0

UNION ALL
SELECT 'FK_|' + OBJECT_NAME(fk.parent_object_id) COLLATE DATABASE_DEFAULT
       + '|' + fk.name COLLATE DATABASE_DEFAULT
       + '|del=' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT
FROM sys.foreign_keys fk
JOIN sys.tables t ON t.object_id = fk.parent_object_id
WHERE t.is_ms_shipped = 0

/* DEFAULT constraints. Added after the first run of this tool reported two schemas
   "identical" while they in fact differed: the pre-consolidation database carried
   DEFAULT 0 on UserProfiles.AllowsDelegationMeeting / .AllowsSpeakerMeeting (left by
   AddUserProfileMeetingFlags' one-time backfill) and a fresh one did not. That
   invisible difference is exactly what crashed the API at boot, because a raw-SQL
   content seed omitted both columns and relied on the default. Compared by column,
   not by constraint name — EF auto-names these (DF__UserProf__Allow__1A2B3C4D) and
   the name differs between any two databases even when the default is the same. */
UNION ALL
SELECT 'DEF|' + OBJECT_NAME(dc.parent_object_id) COLLATE DATABASE_DEFAULT
       + '|' + COL_NAME(dc.parent_object_id, dc.parent_column_id) COLLATE DATABASE_DEFAULT
       + '|' + dc.definition COLLATE DATABASE_DEFAULT
FROM sys.default_constraints dc
JOIN sys.tables t ON t.object_id = dc.parent_object_id
WHERE t.is_ms_shipped = 0
ORDER BY Line;
