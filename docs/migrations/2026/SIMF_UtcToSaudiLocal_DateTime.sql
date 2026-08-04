/*
  SIMF — cutover migration: UTC `datetimeoffset` -> Saudi local wall-clock `datetime2`.

  WHY
    Until 2026-07-31 every timestamp in SIMF was stored as `datetimeoffset(7)` holding
    a UTC instant (offset `+00:00`), and the Saudi reading was produced at the display
    seam. The owner replaced that with a single rule: the system stores, transmits and
    displays ONE number, the Saudi wall clock (`SIMF.Common.SimfClock`, fixed +03:00,
    no DST). Every `DateTimeOffset` became a `DateTime`, so EF now generates
    `datetime2` columns.

    Because that is a CLR type change across ~265 columns, the migrations were
    regenerated as a single consolidated `InitialCreate` per context. A FRESH database
    needs nothing from this file — `MigrateAsync` builds the new schema directly. This
    script exists for the other case: an EXISTING database holding data that must
    carry across.

  WHAT IT DOES, IN ORDER
    Phase 1 — additive schema catch-up. A database that predates the 2026-07-31 build
             wave is missing a handful of tables and columns. They are added here,
             each guarded by IF NOT EXISTS, because Phase 3 tells EF the schema is
             current and EF will never create them afterwards.
    Phase 2 — `SWITCHOFFSET(col, '+03:00')` on every convertible column, then
             `ALTER COLUMN ... datetime2(7)`.
    Phase 3 — repoint the migration history at the consolidated baseline.

  THE CORRECTNESS ARGUMENT, PLAINLY
    SWITCHOFFSET does not move the point in time; it only changes which clock reads
    it. The subsequent ALTER drops the offset and keeps the offset-adjusted reading,
    so `2026-11-20T06:00:00+00:00` lands on `2026-11-20T09:00:00` — what that instant
    always was in Riyadh. `DATEADD(hour, 3, col)` would look similar and would be
    WRONG: it invents a new instant three hours later.

  ONE COLUMN IS DELIBERATELY NOT CONVERTED
    `AspNetUsers.LockoutEnd` is ASP.NET Identity's own property (`IdentityUser`), typed
    `DateTimeOffset?` by the framework and still mapped `datetimeoffset` by the model.
    Converting it would leave the column disagreeing with the model and break lockout
    queries. It is never user-facing, so it stays as it is.

  IDEMPOTENT — by construction, not by a marker row.
    A converted column is `datetime2` and is no longer discovered as `datetimeoffset`;
    every Phase 1 statement is existence-guarded. Re-running converts nothing and only
    re-asserts the history row. There is no double-shift to guard against.

  WHICH DATABASE
    Run against BOTH: the App database (`SimfAppDb`) and the Identity database
    (`SimfIdentityDb`). The script discovers its own columns and detects which of the
    two migration-history tables is present, so the same file is correct for either.
    Phase 1 is App-only and no-ops on Identity.

  BEFORE YOU RUN
    1. Take a verified backup of both databases. This rewrites every timestamp column.
    2. Stop the API, Control Panel and Website. A write landing mid-conversion would be
       written by the OLD code as UTC and then never shifted.
    3. Deploy the API and the mobile app TOGETHER. The wire format changed from `...Z`
       to zone-free local, so an old app against a new API reads three hours out.

  AFTER IT COMMITS — verify, do not assume.
    The final SELECT must report `remaining_datetimeoffset_columns = 0`.
    Then prove the schema really matches the model: build a scratch database from the
    migrations and diff it against the migrated one using
    `tools/qa/schema-fingerprint.sql` (its header gives the exact commands). Expect
    zero differences. If anything is listed, this database started from a state Phase 1
    does not cover — stop and reconcile it rather than shipping.
*/

/* Several affected tables carry FILTERED indexes (IX_SeatReservations_Expires,
   IX_SpeakerMeetingRequests_HallId_SlotStart, ...). SQL Server refuses any UPDATE
   against such a table unless QUOTED_IDENTIFIER and ANSI_NULLS are ON, and sqlcmd —
   unlike SSMS — connects with QUOTED_IDENTIFIER OFF. Setting both here rather than
   relying on a `sqlcmd -I` flag keeps the script correct however it is run. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SaudiOffset char(6) = '+03:00';

/* The consolidated baseline this script hands the database over to. If the migrations
   are ever regenerated these ids change — read them from
   src/Backend/SIMF.Infrastructure/Persistence/Migrations/{App,Identity}/. */
DECLARE @AppMigrationId      nvarchar(150) = N'20260731111632_InitialCreate';
DECLARE @IdentityMigrationId nvarchar(150) = N'20260731111644_InitialCreate';
DECLARE @ProductVersion      nvarchar(32)  = N'10.0.8';

DECLARE @Indexes TABLE (Seq int IDENTITY(1, 1), DropSql nvarchar(max), CreateSql nvarchar(max));
DECLARE @Checks  TABLE (Seq int IDENTITY(1, 1), DropSql nvarchar(max), CreateSql nvarchar(max));
DECLARE @Updates TABLE (Seq int IDENTITY(1, 1), Sql nvarchar(max));
DECLARE @Alters  TABLE (Seq int IDENTITY(1, 1), Sql nvarchar(max));

DECLARE @Seq int, @Max int, @Sql nvarchar(max), @ColumnCount int;

PRINT CONCAT('Database  : ', DB_NAME());

BEGIN TRY
    BEGIN TRANSACTION;

    /* ====================================================================== */
    /* Phase 1 — additive schema catch-up (App database only)                 */
    /* ====================================================================== */

    IF OBJECT_ID('dbo.ExhibitorVisitorScans') IS NOT NULL
       AND COL_LENGTH('dbo.ExhibitorVisitorScans', 'ExhibitorId') IS NULL
    BEGIN
        ALTER TABLE dbo.ExhibitorVisitorScans ADD [ExhibitorId] uniqueidentifier NULL;
        PRINT 'Added     : ExhibitorVisitorScans.ExhibitorId';
    END

    IF OBJECT_ID('dbo.HallSeatLayouts') IS NOT NULL
       AND COL_LENGTH('dbo.HallSeatLayouts', 'SeatTiers') IS NULL
    BEGIN
        ALTER TABLE dbo.HallSeatLayouts ADD [SeatTiers] nvarchar(256) NULL;
        PRINT 'Added     : HallSeatLayouts.SeatTiers';
    END

    IF OBJECT_ID('dbo.SeatReservations') IS NOT NULL
       AND COL_LENGTH('dbo.SeatReservations', 'GuestHint') IS NULL
    BEGIN
        ALTER TABLE dbo.SeatReservations ADD [GuestHint] nvarchar(256) NULL;
        ALTER TABLE dbo.SeatReservations ADD [GuestHintArabic] nvarchar(256) NULL;
        PRINT 'Added     : SeatReservations.GuestHint, .GuestHintArabic';
    END

    IF OBJECT_ID('dbo.SessionQuestions') IS NOT NULL
       AND COL_LENGTH('dbo.SessionQuestions', 'StatusBeforeHidden') IS NULL
    BEGIN
        ALTER TABLE dbo.SessionQuestions ADD [StatusBeforeHidden] int NULL;
        PRINT 'Added     : SessionQuestions.StatusBeforeHidden';
    END

    /* AddUserProfileMeetingFlags added these two NOT NULL bit columns to a populated
       table, so EF emitted a one-time `defaultValue: false` — which SQL Server keeps
       as a permanent DEFAULT constraint. The consolidated InitialCreate builds the
       table from the model instead, and the model declares no default, so a fresh
       database has neither. Dropping them here is what makes a migrated database
       structurally identical to a fresh one, which is the check this script's header
       asks you to run. Safe: EF always supplies both values, and the one raw-SQL
       writer that relied on the default (SIMF_App_SeedGaps.sql) now lists them. */
    IF OBJECT_ID('dbo.UserProfiles') IS NOT NULL
    BEGIN
        DECLARE @DefaultName sysname;
        DECLARE @DropDefault nvarchar(max);
        DECLARE default_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT dc.name
            FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id
                              AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID('dbo.UserProfiles')
              AND c.name IN ('AllowsDelegationMeeting', 'AllowsSpeakerMeeting');
        OPEN default_cur;
        FETCH NEXT FROM default_cur INTO @DefaultName;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @DropDefault = 'ALTER TABLE dbo.UserProfiles DROP CONSTRAINT ' + QUOTENAME(@DefaultName) + ';';
            EXEC sp_executesql @DropDefault;
            PRINT CONCAT('Dropped   : legacy default ', @DefaultName);
            FETCH NEXT FROM default_cur INTO @DefaultName;
        END
        CLOSE default_cur;
        DEALLOCATE default_cur;
    END

    /* FR-1103 device-position pings. Created with datetime2 from the outset: the table
       is new, so it never held a UTC value for Phase 2 to shift. UserId is a bare Guid
       — D-157 forbids a foreign key into the Identity database. */
    IF OBJECT_ID('dbo.Sessions') IS NOT NULL AND OBJECT_ID('dbo.DevicePositionPings') IS NULL
    BEGIN
        CREATE TABLE dbo.DevicePositionPings
        (
            [Id]             uniqueidentifier NOT NULL,
            [UserId]         uniqueidentifier NOT NULL,
            [HallId]         uniqueidentifier NULL,
            [SessionId]      uniqueidentifier NULL,
            [CapturedAt]     datetime2(7)     NOT NULL,
            [Latitude]       float            NOT NULL,
            [Longitude]      float            NOT NULL,
            [AccuracyMeters] float            NULL,
            [CreatedAt]      datetime2(7)     NOT NULL,
            CONSTRAINT [PK_DevicePositionPings] PRIMARY KEY ([Id])
        );
        CREATE NONCLUSTERED INDEX [IX_DevicePositionPings_HallId_CapturedAt]
            ON dbo.DevicePositionPings ([HallId] ASC, [CapturedAt] ASC);
        CREATE NONCLUSTERED INDEX [IX_DevicePositionPings_UserId_CapturedAt]
            ON dbo.DevicePositionPings ([UserId] ASC, [CapturedAt] ASC);
        PRINT 'Added     : DevicePositionPings (+2 indexes)';
    END

    /* The exhibitor-scan uniqueness moved from the scanning USER to the exhibitor
       COMPANY, so the old unique filtered index is replaced by a plain one and a new
       unique index is added on the company column. */
    IF OBJECT_ID('dbo.ExhibitorVisitorScans') IS NOT NULL
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID('dbo.ExhibitorVisitorScans')
                     AND name = 'IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId'
                     AND is_unique = 1)
        BEGIN
            DROP INDEX [IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId]
                ON dbo.ExhibitorVisitorScans;
            CREATE NONCLUSTERED INDEX [IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId]
                ON dbo.ExhibitorVisitorScans ([ExhibitorUserId] ASC, [VisitorUserId] ASC);
            PRINT 'Relaxed   : IX_ExhibitorVisitorScans_ExhibitorUserId_VisitorUserId';
        END

        /* These two statements reference [ExhibitorId], which the ALTER above may have
           created moments ago IN THIS SAME BATCH. SQL Server compiles a batch as a
           unit, so written inline they fail to parse with "Invalid column name
           'ExhibitorId'" before a single statement runs. Dynamic SQL is compiled when
           it executes, by which time the column exists. */
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID('dbo.ExhibitorVisitorScans')
                         AND name = 'IX_ExhibitorVisitorScans_ExhibitorId_VisitorUserId')
        BEGIN
            EXEC sp_executesql N'
                CREATE UNIQUE NONCLUSTERED INDEX [IX_ExhibitorVisitorScans_ExhibitorId_VisitorUserId]
                    ON dbo.ExhibitorVisitorScans ([ExhibitorId] ASC, [VisitorUserId] ASC)
                    WHERE [IsActive] = 1 AND [ExhibitorId] IS NOT NULL;';
            PRINT 'Added     : IX_ExhibitorVisitorScans_ExhibitorId_VisitorUserId';
        END

        IF OBJECT_ID('dbo.Exhibitors') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys
                           WHERE name = 'FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId')
        BEGIN
            EXEC sp_executesql N'
                ALTER TABLE dbo.ExhibitorVisitorScans
                    ADD CONSTRAINT [FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId]
                    FOREIGN KEY ([ExhibitorId]) REFERENCES dbo.Exhibitors ([Id]);';
            PRINT 'Added     : FK_ExhibitorVisitorScans_Exhibitors_ExhibitorId';
        END
    END

    /* ====================================================================== */
    /* Phase 2 — UTC instants -> Saudi wall clock                             */
    /* ====================================================================== */

    /* Resolved ONCE and reused by every step below. Four separate filters that each
       had to remember the same exclusions would eventually drift apart; one list
       cannot. */
    CREATE TABLE #Convertible
    (
        SchemaName sysname,
        TableName  sysname,
        ColumnName sysname,
        IsNullable bit
    );

    INSERT INTO #Convertible (SchemaName, TableName, ColumnName, IsNullable)
    SELECT s.name, t.name, c.name, c.is_nullable
    FROM sys.columns c
    JOIN sys.tables t  ON t.object_id = c.object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE c.system_type_id = 43              /* datetimeoffset */
      AND t.is_ms_shipped = 0
      AND t.name NOT LIKE 'plan_persist%'    /* Query Store internals */
      /* Owned by ASP.NET Identity and still `datetimeoffset` in the model. */
      AND NOT (t.name = 'AspNetUsers' AND c.name = 'LockoutEnd');

    SELECT @ColumnCount = COUNT(*) FROM #Convertible;
    PRINT CONCAT('To convert: ', @ColumnCount, ' datetimeoffset column(s)');

    IF @ColumnCount > 0
    BEGIN
        /* ---- 2a. Capture the objects that block ALTER COLUMN ----------------- */

        INSERT INTO @Indexes (DropSql, CreateSql)
        SELECT
            CONCAT('DROP INDEX ', QUOTENAME(i.name), ' ON ',
                   QUOTENAME(s.name), '.', QUOTENAME(t.name), ';'),
            CONCAT('CREATE ',
                   CASE WHEN i.is_unique = 1 THEN 'UNIQUE ' ELSE '' END,
                   i.type_desc, ' INDEX ', QUOTENAME(i.name), ' ON ',
                   QUOTENAME(s.name), '.', QUOTENAME(t.name), ' (',
                   (SELECT STRING_AGG(CONCAT(QUOTENAME(kc.name),
                               CASE WHEN kic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END), ', ')
                               WITHIN GROUP (ORDER BY kic.key_ordinal)
                    FROM sys.index_columns kic
                    JOIN sys.columns kc ON kc.object_id = kic.object_id AND kc.column_id = kic.column_id
                    WHERE kic.object_id = i.object_id AND kic.index_id = i.index_id
                      AND kic.is_included_column = 0),
                   ')',
                   /* `+` here, deliberately, NOT CONCAT: CONCAT treats NULL as an empty
                      string, so an index with no INCLUDE columns would build
                      ' INCLUDE ()' and an unfiltered index ' WHERE ' — both syntax
                      errors. With `+` the whole term collapses to NULL and ISNULL
                      drops it. */
                   ISNULL(' INCLUDE (' +
                       (SELECT STRING_AGG(QUOTENAME(nc.name), ', ') WITHIN GROUP (ORDER BY nic.index_column_id)
                        FROM sys.index_columns nic
                        JOIN sys.columns nc ON nc.object_id = nic.object_id AND nc.column_id = nic.column_id
                        WHERE nic.object_id = i.object_id AND nic.index_id = i.index_id
                          AND nic.is_included_column = 1)
                       + ')', ''),
                   ISNULL(' WHERE ' + i.filter_definition, ''),
                   ';')
        FROM sys.indexes i
        JOIN sys.tables t  ON t.object_id = i.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE i.type IN (1, 2)                 /* clustered / nonclustered */
          AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND (
                /* the column is a key or an INCLUDE column */
                EXISTS (SELECT 1
                        FROM sys.index_columns x
                        JOIN sys.columns xc ON xc.object_id = x.object_id AND xc.column_id = x.column_id
                        JOIN #Convertible cv ON cv.TableName = t.name AND cv.ColumnName = xc.name
                        WHERE x.object_id = i.object_id AND x.index_id = i.index_id)
                /* ...or it appears ONLY in a filter, e.g.
                   IX_SeatReservations_SessionId_RowLabel_SeatNumber WHERE ([ReleasedAt] IS NULL).
                   sys.index_columns does not report filter references, so such an index
                   is invisible to the check above and would fail the ALTER.
                   CHARINDEX, not LIKE: a bracketed name like [Leave] is a character
                   class in a LIKE pattern and would not match literally. */
             OR EXISTS (SELECT 1
                        FROM #Convertible cv
                        WHERE cv.TableName = t.name
                          AND i.filter_definition IS NOT NULL
                          AND CHARINDEX(QUOTENAME(cv.ColumnName), i.filter_definition) > 0)
             );

        /* Every CHECK constraint on an affected table, not only those whose text
           mentions a timestamp column. Matching constraint definitions by substring is
           fragile (a column named [Start] matches half of them), and dropping a few
           extra costs nothing because they are all re-added below. */
        INSERT INTO @Checks (DropSql, CreateSql)
        SELECT
            CONCAT('ALTER TABLE ', QUOTENAME(s.name), '.', QUOTENAME(t.name),
                   ' DROP CONSTRAINT ', QUOTENAME(cc.name), ';'),
            CONCAT('ALTER TABLE ', QUOTENAME(s.name), '.', QUOTENAME(t.name),
                   ' WITH CHECK ADD CONSTRAINT ', QUOTENAME(cc.name), ' CHECK ', cc.definition, ';')
        FROM sys.check_constraints cc
        JOIN sys.tables t  ON t.object_id = cc.parent_object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE cc.is_ms_shipped = 0
          AND EXISTS (SELECT 1 FROM #Convertible cv WHERE cv.TableName = t.name);

        /* ---- 2b. Build the conversion statements ---------------------------- */

        /* One UPDATE per table rather than per column: a table with six timestamp
           columns is scanned once instead of six times. SWITCHOFFSET(NULL) is NULL, so
           nullable columns need no guard. */
        INSERT INTO @Updates (Sql)
        SELECT CONCAT('UPDATE ', QUOTENAME(SchemaName), '.', QUOTENAME(TableName), ' SET ',
                      STRING_AGG(CONCAT(QUOTENAME(ColumnName), ' = SWITCHOFFSET(', QUOTENAME(ColumnName),
                                        ', ''', @SaudiOffset, ''')'), ', '), ';')
        FROM #Convertible
        GROUP BY SchemaName, TableName;

        INSERT INTO @Alters (Sql)
        SELECT CONCAT('ALTER TABLE ', QUOTENAME(SchemaName), '.', QUOTENAME(TableName),
                      ' ALTER COLUMN ', QUOTENAME(ColumnName), ' datetime2(7) ',
                      CASE WHEN IsNullable = 1 THEN 'NULL' ELSE 'NOT NULL' END, ';')
        FROM #Convertible;

        /* ---- 2c. Drop blockers ---------------------------------------------- */

        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Indexes;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = DropSql FROM @Indexes WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Dropped   : ', @Max, ' index(es)');

        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Checks;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = DropSql FROM @Checks WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Dropped   : ', @Max, ' check constraint(s)');

        /* ---- 2d. Re-express the instants, then change the type -------------- */

        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Updates;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = Sql FROM @Updates WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Shifted   : ', @Max, ' table(s) to ', @SaudiOffset);

        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Alters;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = Sql FROM @Alters WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Converted : ', @Max, ' column(s) to datetime2(7)');

        /* ---- 2e. Restore the blockers --------------------------------------- */

        /* WITH CHECK on purpose: this revalidates every row, so an ordering the shift
           broke aborts the transaction instead of committing silently. Constraints
           like CK_Sessions_TimeWindow ([End] > [Start]) are the proof. */
        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Checks;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = CreateSql FROM @Checks WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Revalidated: ', @Max, ' check constraint(s)');

        SELECT @Seq = 1, @Max = ISNULL(MAX(Seq), 0) FROM @Indexes;
        WHILE @Seq <= @Max
        BEGIN
            SELECT @Sql = CreateSql FROM @Indexes WHERE Seq = @Seq;
            EXEC sp_executesql @Sql;
            SET @Seq += 1;
        END
        PRINT CONCAT('Rebuilt   : ', @Max, ' index(es)');
    END
    ELSE
    BEGIN
        PRINT 'Nothing to convert — already on Saudi local time.';
    END

    DROP TABLE #Convertible;

    /* ====================================================================== */
    /* Phase 3 — hand the database over to the consolidated baseline          */
    /* ====================================================================== */

    /* The pre-cutover history lists the superseded per-feature migrations, none of
       which exist in the assembly any more; EF would otherwise try to replay
       InitialCreate over a populated database. Replacing the history with the single
       consolidated id tells EF the schema is current — which it now is, Phase 1 having
       closed the schema gap and Phase 2 the type gap. Runs unconditionally so a re-run
       after a partial deploy still lands correctly. */
    IF OBJECT_ID('dbo.__EFMigrationsHistory_App') IS NOT NULL
    BEGIN
        DELETE FROM dbo.__EFMigrationsHistory_App;
        INSERT INTO dbo.__EFMigrationsHistory_App (MigrationId, ProductVersion)
        VALUES (@AppMigrationId, @ProductVersion);
        PRINT CONCAT('History   : App -> ', @AppMigrationId);
    END

    IF OBJECT_ID('dbo.__EFMigrationsHistory_Identity') IS NOT NULL
    BEGIN
        DELETE FROM dbo.__EFMigrationsHistory_Identity;
        INSERT INTO dbo.__EFMigrationsHistory_Identity (MigrationId, ProductVersion)
        VALUES (@IdentityMigrationId, @ProductVersion);
        PRINT CONCAT('History   : Identity -> ', @IdentityMigrationId);
    END

    COMMIT TRANSACTION;
    PRINT 'COMMITTED.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    PRINT 'ROLLED BACK — the database is unchanged.';
    THROW;
END CATCH

/* ---- Report -------------------------------------------------------------- */
/* AspNetUsers.LockoutEnd is excluded: it is the one column the model still maps as
   datetimeoffset, so on the Identity database the expected answer is 0 here and
   exactly one surviving datetimeoffset column in the table. */
SELECT COUNT(*) AS remaining_datetimeoffset_columns
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
WHERE c.system_type_id = 43
  AND t.is_ms_shipped = 0
  AND t.name NOT LIKE 'plan_persist%'
  AND NOT (t.name = 'AspNetUsers' AND c.name = 'LockoutEnd');
GO
