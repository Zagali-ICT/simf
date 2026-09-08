-- =====================================================================
-- SIMF - schema inventory for a database whose exact schema is unknown.
--
-- WHY THIS EXISTS
--
-- Until 2026-09-07 every schema change regenerated ONE migration at the
-- pinned id 00000000000000_InitialCreate. A regenerated migration never
-- reaches a database that already has that history row, so each database
-- kept whatever schema it was BUILT from - while all of them report the
-- same migration id. The id therefore identifies a row, not a schema, and
-- two databases can both say 00000000000000_InitialCreate and differ.
--
-- That is not hypothetical. On 2026-09-07 the API refused to start against
-- production because a migration tried to drop
-- FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId,
-- which exists in the developer baseline and does NOT exist in production.
--
-- So before ANY migration is written for a database, its real schema has to
-- be established rather than assumed. sqlpackage /Action:Extract is the
-- better tool where it is installed; this script is the fallback that needs
-- nothing but sqlcmd.
--
-- HOW TO RUN (read-only; it writes nothing and takes no locks worth the name)
--
--   sqlcmd -S <server> -d SIMF_App -E -C -s "|" -W -h -1 ^
--          -i tools\migrations\Dump-SchemaInventory.sql ^
--          -o SIMF_App-inventory.txt
--
-- Repeat with -d SIMF_Identity. Send both files back. They contain schema
-- only - object names, types, nullability, keys - and NO row data, so they
-- carry nothing confidential beyond the shape of the database.
-- =====================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

PRINT '### DATABASE';
SELECT DB_NAME() AS [database], CONVERT(varchar(40), SERVERPROPERTY('ProductVersion')) AS sql_version;

PRINT '';
PRINT '### MIGRATION HISTORY';
-- The history table is named per context, so look for either.
IF OBJECT_ID(N'dbo.__EFMigrationsHistory_App') IS NOT NULL
    SELECT 'App' AS ctx, MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory_App ORDER BY MigrationId;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory_Identity') IS NOT NULL
    SELECT 'Identity' AS ctx, MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory_Identity ORDER BY MigrationId;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL
    SELECT 'default' AS ctx, MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

PRINT '';
PRINT '### COLUMNS  table|column|type|max_length|precision|scale|nullable|identity|computed|collation|default';
SELECT
    t.name                                            AS [table],
    c.name                                            AS [column],
    ty.name                                           AS [type],
    c.max_length, c.precision, c.scale,
    c.is_nullable, c.is_identity, c.is_computed,
    ISNULL(c.collation_name, '')                      AS collation,
    ISNULL(dc.definition, '')                         AS [default]
FROM sys.columns c
JOIN sys.tables t            ON t.object_id = c.object_id
JOIN sys.types ty            ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc
       ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY t.name, c.name;

PRINT '';
PRINT '### INDEXES  table|index|type|unique|primary_key|key_columns|included|filter';
SELECT
    t.name        AS [table],
    i.name        AS [index],
    i.type_desc   AS [type],
    i.is_unique, i.is_primary_key,
    ISNULL(STUFF((SELECT ',' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
                  FROM sys.index_columns ic
                  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND ic.is_included_column = 0
                  ORDER BY ic.key_ordinal
                  FOR XML PATH('')), 1, 1, ''), '') AS key_columns,
    ISNULL(STUFF((SELECT ',' + c.name
                  FROM sys.index_columns ic
                  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                    AND ic.is_included_column = 1
                  ORDER BY c.name
                  FOR XML PATH('')), 1, 1, ''), '') AS included,
    ISNULL(i.filter_definition, '') AS [filter]
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.is_ms_shipped = 0 AND i.name IS NOT NULL
ORDER BY t.name, i.name;

PRINT '';
PRINT '### FOREIGN KEYS  name|from_table|from_columns|to_table|to_columns|on_delete|on_update';
SELECT
    fk.name              AS [name],
    tp.name              AS from_table,
    ISNULL(STUFF((SELECT ',' + cp.name
                  FROM sys.foreign_key_columns fkc
                  JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
                  WHERE fkc.constraint_object_id = fk.object_id
                  ORDER BY fkc.constraint_column_id
                  FOR XML PATH('')), 1, 1, ''), '') AS from_columns,
    tr.name              AS to_table,
    ISNULL(STUFF((SELECT ',' + cr.name
                  FROM sys.foreign_key_columns fkc
                  JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
                  WHERE fkc.constraint_object_id = fk.object_id
                  ORDER BY fkc.constraint_column_id
                  FOR XML PATH('')), 1, 1, ''), '') AS to_columns,
    fk.delete_referential_action_desc AS on_delete,
    fk.update_referential_action_desc AS on_update
FROM sys.foreign_keys fk
JOIN sys.tables tp ON tp.object_id = fk.parent_object_id
JOIN sys.tables tr ON tr.object_id = fk.referenced_object_id
ORDER BY fk.name;

PRINT '';
PRINT '### CHECK CONSTRAINTS  table|name|definition';
SELECT t.name AS [table], cc.name AS [name], cc.definition
FROM sys.check_constraints cc
JOIN sys.tables t ON t.object_id = cc.parent_object_id
ORDER BY t.name, cc.name;

PRINT '';
PRINT '### PRIMARY / UNIQUE KEY CONSTRAINTS  table|name|type';
SELECT t.name AS [table], kc.name AS [name], kc.type_desc AS [type]
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
ORDER BY t.name, kc.name;

PRINT '';
PRINT '### SEQUENCES';
SELECT name, CONVERT(varchar(40), current_value) AS current_value FROM sys.sequences ORDER BY name;

PRINT '';
PRINT '### ROW COUNTS (shape only - how much data a change would touch, no values)';
SELECT t.name AS [table], SUM(p.rows) AS [rows]
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0
GROUP BY t.name
ORDER BY t.name;

PRINT '';
PRINT '### END OF INVENTORY';
