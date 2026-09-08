-- =====================================================================
-- SIMF - one line per table, so two databases can be compared by eye or by
-- diff without moving 1700 lines around.
--
-- WHY A FINGERPRINT
--
-- Until 2026-09-07 every schema change regenerated ONE migration at the
-- pinned id 00000000000000_InitialCreate. A regenerated migration never
-- reaches a database that already has that history row, so each database
-- kept whatever schema it was BUILT from while all of them report the same
-- migration id. The id identifies a row, not a schema.
--
-- Two databases can therefore both say 00000000000000_InitialCreate and
-- differ - which is exactly how the API came to fail on production trying to
-- drop a foreign key that only exists on a developer machine. Before writing
-- a migration for such a database its real schema has to be established.
--
-- This is the cheap first pass: a hash per table over its columns, indexes,
-- foreign keys and check constraints. Compare the output against the same
-- query run elsewhere; the tables whose hashes differ are the only ones worth
-- looking at in detail, and Dump-SchemaInventory-SSMS.sql gives that detail.
--
-- HOW TO RUN
--   Open a query window on SIMF_App (then SIMF_Identity), execute, click the
--   column header to select the whole column, Ctrl+C.
--
-- Reads system catalogue views only. Writes nothing, returns NO row data -
-- names, types, keys, and row COUNTS only.
--
--   DB   | database | sql version
--   MIG  | context | migration id | product version
--   TBL  | table | ncols | nidx | nfk | nchk | rows | hash
--   TOT  | tables | columns | indexes | fks | checks | hash over everything
-- =====================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- The catalogue collation and the database collation are usually different,
-- and a collation is part of an expression's type, so every catalogue operand
-- is forced to one side before it is concatenated.
;WITH cols AS
(
    SELECT t.object_id,
           c.name COLLATE DATABASE_DEFAULT AS col,
           ty.name COLLATE DATABASE_DEFAULT
             + N':' + CONVERT(nvarchar(12), c.max_length)
             + N':' + CONVERT(nvarchar(6), c.precision)
             + N':' + CONVERT(nvarchar(6), c.scale)
             + N':' + CONVERT(nvarchar(1), c.is_nullable)
             + N':' + ISNULL(c.collation_name COLLATE DATABASE_DEFAULT, N'-') AS spec
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE t.is_ms_shipped = 0
),
idx AS
(
    SELECT i.object_id,
           i.name COLLATE DATABASE_DEFAULT
             + N':' + CONVERT(nvarchar(1), i.is_unique)
             + N':' + ISNULL(i.filter_definition COLLATE DATABASE_DEFAULT, N'-') AS spec
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    WHERE t.is_ms_shipped = 0 AND i.name IS NOT NULL
),
fks AS
(
    SELECT fk.parent_object_id AS object_id,
           fk.name COLLATE DATABASE_DEFAULT
             + N':' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT AS spec
    FROM sys.foreign_keys fk
),
chk AS
(
    SELECT cc.parent_object_id AS object_id,
           cc.name COLLATE DATABASE_DEFAULT AS spec
    FROM sys.check_constraints cc
),
per_table AS
(
    SELECT
        t.object_id,
        t.name COLLATE DATABASE_DEFAULT AS tbl,
        (SELECT COUNT(*) FROM cols WHERE cols.object_id = t.object_id)  AS ncols,
        (SELECT COUNT(*) FROM idx  WHERE idx.object_id  = t.object_id)  AS nidx,
        (SELECT COUNT(*) FROM fks  WHERE fks.object_id  = t.object_id)  AS nfk,
        (SELECT COUNT(*) FROM chk  WHERE chk.object_id  = t.object_id)  AS nchk,
        ISNULL((SELECT SUM(p.rows) FROM sys.partitions p
                 WHERE p.object_id = t.object_id AND p.index_id IN (0,1)), 0) AS nrows,
        -- Sorted, so two databases that built the same objects in a different
        -- order still hash identically.
        ISNULL(STUFF((SELECT N',' + col + N'=' + spec FROM cols
                       WHERE cols.object_id = t.object_id ORDER BY col
                       FOR XML PATH('')), 1, 1, N''), N'')
      + N'#' + ISNULL(STUFF((SELECT N',' + spec FROM idx
                       WHERE idx.object_id = t.object_id ORDER BY spec
                       FOR XML PATH('')), 1, 1, N''), N'')
      + N'#' + ISNULL(STUFF((SELECT N',' + spec FROM fks
                       WHERE fks.object_id = t.object_id ORDER BY spec
                       FOR XML PATH('')), 1, 1, N''), N'')
      + N'#' + ISNULL(STUFF((SELECT N',' + spec FROM chk
                       WHERE chk.object_id = t.object_id ORDER BY spec
                       FOR XML PATH('')), 1, 1, N''), N'') AS shape
    FROM sys.tables t
    WHERE t.is_ms_shipped = 0
),
body AS
(
    SELECT 1 AS grp,
           N'TBL|' + tbl
             + N'|' + CONVERT(nvarchar(6), ncols)
             + N'|' + CONVERT(nvarchar(6), nidx)
             + N'|' + CONVERT(nvarchar(6), nfk)
             + N'|' + CONVERT(nvarchar(6), nchk)
             + N'|' + CONVERT(nvarchar(20), nrows)
             + N'|' + LOWER(CONVERT(varchar(18), HASHBYTES('SHA2_256', shape), 1)) AS line,
           tbl AS sortkey
    FROM per_table
)
SELECT line AS [-- select this whole column and copy --]
FROM (
    SELECT 0 AS grp, N'DB|' + DB_NAME() + N'|' + CONVERT(nvarchar(40), SERVERPROPERTY('ProductVersion')) AS line, N'' AS sortkey
    UNION ALL SELECT grp, line, sortkey FROM body
    UNION ALL
    SELECT 2,
           N'TOT|' + CONVERT(nvarchar(6), COUNT(*))
             + N'|' + CONVERT(nvarchar(8), SUM(ncols))
             + N'|' + CONVERT(nvarchar(8), SUM(nidx))
             + N'|' + CONVERT(nvarchar(8), SUM(nfk))
             + N'|' + CONVERT(nvarchar(8), SUM(nchk)), N''
    FROM per_table
) x
ORDER BY grp, sortkey;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory_App') IS NOT NULL
    SELECT N'MIG|App|' + MigrationId + N'|' + ProductVersion AS [-- and this --]
    FROM dbo.__EFMigrationsHistory_App ORDER BY MigrationId;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory_Identity') IS NOT NULL
    SELECT N'MIG|Identity|' + MigrationId + N'|' + ProductVersion AS [-- and this --]
    FROM dbo.__EFMigrationsHistory_Identity ORDER BY MigrationId;
