-- =====================================================================
-- SIMF - schema inventory as ONE copyable column, for SSMS.
--
-- Same purpose as Dump-SchemaInventory.sql, different shape: that one is
-- for sqlcmd and writes several result sets to a file. This one returns a
-- SINGLE result set with a SINGLE column, so it can be selected in the
-- SSMS grid and pasted somewhere in one action.
--
-- HOW TO RUN
--   1. Open a query window on the database (SIMF_App, then SIMF_Identity).
--   2. Execute.
--   3. Click the column header to select the whole column, Ctrl+C, paste.
--      (Ctrl+T first - "Results to Text" - if you prefer plain text.)
--
-- It reads system catalogue views only. It writes nothing, changes nothing,
-- and returns NO row data - object names, types, nullability, keys and row
-- COUNTS only.
--
-- Line format, so the output can be diffed mechanically:
--   DB  | database | sql version
--   MIG | context | migration id | product version
--   T   | table | column count | row count
--   C   | table | column | type | nullable | identity | computed | collation | default
--   IX  | table | index | UNIQUE/- | PK/- | key columns | included | filter
--   FK  | name | from table | from columns | to table | to columns | on delete
--   CK  | table | name | definition
--   KC  | table | name | PRIMARY_KEY/UNIQUE
--   SEQ | name
-- =====================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

;WITH lines AS
(
    SELECT 0 AS grp, 0 AS ord, CONVERT(nvarchar(4000),
           N'DB|' + DB_NAME() + N'|' + CONVERT(nvarchar(40), SERVERPROPERTY('ProductVersion'))) AS line

    -- Tables: column count and row count, so a missing column shows up as a
    -- count difference even before the per-column lines are compared.
    UNION ALL
    SELECT 3, 0,
           N'T|' + t.name COLLATE DATABASE_DEFAULT
             + N'|' + CONVERT(nvarchar(10), (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id))
             + N'|' + CONVERT(nvarchar(20), ISNULL((SELECT SUM(p.rows) FROM sys.partitions p
                                                     WHERE p.object_id = t.object_id AND p.index_id IN (0,1)), 0))
    FROM sys.tables t WHERE t.is_ms_shipped = 0

    UNION ALL
    SELECT 4, 0,
           N'C|' + t.name COLLATE DATABASE_DEFAULT + N'|' + c.name COLLATE DATABASE_DEFAULT + N'|' + ty.name COLLATE DATABASE_DEFAULT
             + CASE WHEN ty.name COLLATE DATABASE_DEFAULT IN (N'nvarchar', N'varchar', N'nchar', N'char', N'varbinary', N'binary')
                    THEN N'(' + CASE WHEN c.max_length = -1 THEN N'max'
                                     WHEN ty.name COLLATE DATABASE_DEFAULT IN (N'nvarchar', N'nchar') THEN CONVERT(nvarchar(10), c.max_length / 2)
                                     ELSE CONVERT(nvarchar(10), c.max_length) END + N')'
                    WHEN ty.name COLLATE DATABASE_DEFAULT IN (N'decimal', N'numeric')
                    THEN N'(' + CONVERT(nvarchar(10), c.precision) + N',' + CONVERT(nvarchar(10), c.scale) + N')'
                    ELSE N'' END
             + N'|' + CASE WHEN c.is_nullable = 1 THEN N'NULL' ELSE N'NOT NULL' END
             + N'|' + CASE WHEN c.is_identity = 1 THEN N'IDENTITY' ELSE N'-' END
             + N'|' + CASE WHEN c.is_computed = 1 THEN N'COMPUTED' ELSE N'-' END
             + N'|' + ISNULL(c.collation_name COLLATE DATABASE_DEFAULT, N'-')
             + N'|' + ISNULL(dc.definition COLLATE DATABASE_DEFAULT, N'-')
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    LEFT JOIN sys.default_constraints dc
           ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE t.is_ms_shipped = 0

    UNION ALL
    SELECT 5, 0,
           N'IX|' + t.name COLLATE DATABASE_DEFAULT + N'|' + i.name COLLATE DATABASE_DEFAULT
             + N'|' + CASE WHEN i.is_unique = 1 THEN N'UNIQUE' ELSE N'-' END
             + N'|' + CASE WHEN i.is_primary_key = 1 THEN N'PK' ELSE N'-' END
             + N'|' + ISNULL(STUFF((SELECT N',' + c.name COLLATE DATABASE_DEFAULT + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END
                                    FROM sys.index_columns ic
                                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                                      AND ic.is_included_column = 0
                                    ORDER BY ic.key_ordinal
                                    FOR XML PATH('')), 1, 1, N''), N'-')
             + N'|' + ISNULL(STUFF((SELECT N',' + c.name COLLATE DATABASE_DEFAULT
                                    FROM sys.index_columns ic
                                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                                    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                                      AND ic.is_included_column = 1
                                    ORDER BY c.name
                                    FOR XML PATH('')), 1, 1, N''), N'-')
             + N'|' + ISNULL(i.filter_definition COLLATE DATABASE_DEFAULT, N'-')
    FROM sys.indexes i
    JOIN sys.tables t ON t.object_id = i.object_id
    WHERE t.is_ms_shipped = 0 AND i.name IS NOT NULL

    UNION ALL
    SELECT 6, 0,
           N'FK|' + fk.name COLLATE DATABASE_DEFAULT + N'|' + tp.name COLLATE DATABASE_DEFAULT
             + N'|' + ISNULL(STUFF((SELECT N',' + cp.name COLLATE DATABASE_DEFAULT
                                    FROM sys.foreign_key_columns fkc
                                    JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
                                    WHERE fkc.constraint_object_id = fk.object_id
                                    ORDER BY fkc.constraint_column_id
                                    FOR XML PATH('')), 1, 1, N''), N'-')
             + N'|' + tr.name COLLATE DATABASE_DEFAULT
             + N'|' + ISNULL(STUFF((SELECT N',' + cr.name COLLATE DATABASE_DEFAULT
                                    FROM sys.foreign_key_columns fkc
                                    JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
                                    WHERE fkc.constraint_object_id = fk.object_id
                                    ORDER BY fkc.constraint_column_id
                                    FOR XML PATH('')), 1, 1, N''), N'-')
             + N'|' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT
    FROM sys.foreign_keys fk
    JOIN sys.tables tp ON tp.object_id = fk.parent_object_id
    JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id

    UNION ALL
    SELECT 7, 0, N'CK|' + t.name COLLATE DATABASE_DEFAULT + N'|' + cc.name COLLATE DATABASE_DEFAULT + N'|' + REPLACE(REPLACE(cc.definition COLLATE DATABASE_DEFAULT, CHAR(13), N' '), CHAR(10), N' ')
    FROM sys.check_constraints cc
    JOIN sys.tables t ON t.object_id = cc.parent_object_id

    UNION ALL
    SELECT 8, 0, N'KC|' + t.name COLLATE DATABASE_DEFAULT + N'|' + kc.name COLLATE DATABASE_DEFAULT + N'|' + kc.type_desc COLLATE DATABASE_DEFAULT
    FROM sys.key_constraints kc
    JOIN sys.tables t ON t.object_id = kc.parent_object_id

    UNION ALL
    SELECT 9, 0, N'SEQ|' + name COLLATE DATABASE_DEFAULT FROM sys.sequences
)
SELECT line AS [-- copy this whole column --]
FROM lines
ORDER BY grp, line;

-- The migration history is a separate statement: the table is named per
-- context and may not exist, so it cannot sit inside the UNION above.
IF OBJECT_ID(N'dbo.__EFMigrationsHistory_App') IS NOT NULL
    SELECT N'MIG|App|' + MigrationId COLLATE DATABASE_DEFAULT + N'|' + ProductVersion COLLATE DATABASE_DEFAULT AS [-- and this one --]
    FROM dbo.__EFMigrationsHistory_App ORDER BY MigrationId;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory_Identity') IS NOT NULL
    SELECT N'MIG|Identity|' + MigrationId COLLATE DATABASE_DEFAULT + N'|' + ProductVersion COLLATE DATABASE_DEFAULT AS [-- and this one --]
    FROM dbo.__EFMigrationsHistory_Identity ORDER BY MigrationId;
