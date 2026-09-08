using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <summary>
    /// Brings a database up to the current model from ANY of the schemas the pinned
    /// migration id left behind.
    ///
    /// <para><b>Why every statement is guarded.</b> Until 2026-09-07 a schema change
    /// regenerated one migration at a pinned id. A regenerated migration never
    /// reaches a database that already has that history row, so each database kept
    /// whatever schema it was BUILT from while all of them report the same id: the
    /// id identifies a row, not a schema. Measured, not assumed - the developer pair
    /// sits at the 2026-08-14 baseline (88 tables, 283 indexes, 81 foreign keys) and
    /// production sits at the model minus one lift (90 tables, 311 indexes, 103
    /// foreign keys). An unguarded migration written against one of them fails on
    /// the other, which is exactly what happened: production refused to start on a
    /// DROP CONSTRAINT for a foreign key only the developer baseline has.
    ///
    /// So each operation carries its own existence check and this migration
    /// CONVERGES rather than replays. On the developer pair it applies the whole
    /// delta; on production it skips what is already there and applies the
    /// remainder. The DDL is EF's own, generated from the model and then guarded -
    /// not hand-authored.</para>
    ///
    /// <para><b>The one thing a guard alone cannot fix.</b>
    /// <c>IX_Sponsors_Tier_NameArabic</c> is new in the model, so the generated
    /// script only CREATEs it - from the baseline there is nothing to drop.
    /// Production already has that index, and SQL Server will not change a column's
    /// collation while an index references it, so the collation change on
    /// <c>Sponsors.NameArabic</c> would fail there. A guarded DROP is inserted ahead
    /// of it and the CREATE further down puts it back. The two other indexes over
    /// collated columns are dropped and recreated by EF itself, which is why they
    /// were never a problem.</para>
    /// </summary>
    public partial class SyncBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // dropconstraint FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId' AND parent_object_id = OBJECT_ID(N'[DelegationMeetingRequests]'))
BEGIN
    ALTER TABLE [DelegationMeetingRequests] DROP CONSTRAINT [FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId];
END
");

            // dropconstraint FK_GateScans_UserProfiles_UserProfileId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_GateScans_UserProfiles_UserProfileId' AND parent_object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    ALTER TABLE [GateScans] DROP CONSTRAINT [FK_GateScans_UserProfiles_UserProfileId];
END
");

            // dropconstraint FK_HallAttendances_Halls_HallId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_HallAttendances_Halls_HallId' AND parent_object_id = OBJECT_ID(N'[HallAttendances]'))
BEGIN
    ALTER TABLE [HallAttendances] DROP CONSTRAINT [FK_HallAttendances_Halls_HallId];
END
");

            // dropindex IX_VisitorShareTokens_Token
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitorShareTokens_Token' AND object_id = OBJECT_ID(N'[VisitorShareTokens]'))
BEGIN
    DROP INDEX [IX_VisitorShareTokens_Token] ON [VisitorShareTokens];
END
");

            // dropindex IX_UserProfiles_IqamaNumberHash
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserProfiles_IqamaNumberHash' AND object_id = OBJECT_ID(N'[UserProfiles]'))
BEGIN
    DROP INDEX [IX_UserProfiles_IqamaNumberHash] ON [UserProfiles];
END
");

            // dropindex IX_UserProfiles_NationalIdHash
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserProfiles_NationalIdHash' AND object_id = OBJECT_ID(N'[UserProfiles]'))
BEGIN
    DROP INDEX [IX_UserProfiles_NationalIdHash] ON [UserProfiles];
END
");

            // dropindex IX_UserProfiles_PassportNumberHash
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserProfiles_PassportNumberHash' AND object_id = OBJECT_ID(N'[UserProfiles]'))
BEGIN
    DROP INDEX [IX_UserProfiles_PassportNumberHash] ON [UserProfiles];
END
");

            // dropindex IX_Speakers_UserProfileId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Speakers_UserProfileId' AND object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    DROP INDEX [IX_Speakers_UserProfileId] ON [Speakers];
END
");

            // dropindex IX_SessionSummaries_IsActive_PublishedAt
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionSummaries_IsActive_PublishedAt' AND object_id = OBJECT_ID(N'[SessionSummaries]'))
BEGIN
    DROP INDEX [IX_SessionSummaries_IsActive_PublishedAt] ON [SessionSummaries];
END
");

            // dropindex IX_SessionQuestions_SessionId_IsHidden_Order
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionQuestions_SessionId_IsHidden_Order' AND object_id = OBJECT_ID(N'[SessionQuestions]'))
BEGIN
    DROP INDEX [IX_SessionQuestions_SessionId_IsHidden_Order] ON [SessionQuestions];
END
");

            // dropindex IX_SessionQuestions_SubmittedByUserId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionQuestions_SubmittedByUserId' AND object_id = OBJECT_ID(N'[SessionQuestions]'))
BEGIN
    DROP INDEX [IX_SessionQuestions_SubmittedByUserId] ON [SessionQuestions];
END
");

            // dropindex IX_SessionOutcomes_SessionId_IsActive_DisplayOrder
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionOutcomes_SessionId_IsActive_DisplayOrder' AND object_id = OBJECT_ID(N'[SessionOutcomes]'))
BEGIN
    DROP INDEX [IX_SessionOutcomes_SessionId_IsActive_DisplayOrder] ON [SessionOutcomes];
END
");

            // dropindex IX_SeatReservations_Expires
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeatReservations_Expires' AND object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    DROP INDEX [IX_SeatReservations_Expires] ON [SeatReservations];
END
");

            // dropconstraint CK_Halls_Geofence
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Halls_Geofence' AND parent_object_id = OBJECT_ID(N'[Halls]'))
BEGIN
    ALTER TABLE [Halls] DROP CONSTRAINT [CK_Halls_Geofence];
END
");

            // dropindex IX_HallAttendances_HallId_Leave
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HallAttendances_HallId_Leave' AND object_id = OBJECT_ID(N'[HallAttendances]'))
BEGIN
    DROP INDEX [IX_HallAttendances_HallId_Leave] ON [HallAttendances];
END
");

            // dropindex IX_DelegationMeetingRequests_AvailabilityWindowId
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DelegationMeetingRequests_AvailabilityWindowId' AND object_id = OBJECT_ID(N'[DelegationMeetingRequests]'))
BEGIN
    DROP INDEX [IX_DelegationMeetingRequests_AvailabilityWindowId] ON [DelegationMeetingRequests];
END
");

            // drop Sponsors.LogoRelativePath
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'LogoRelativePath') IS NOT NULL
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sponsors]') AND [c].[name] = N'LogoRelativePath');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Sponsors] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Sponsors] DROP COLUMN [LogoRelativePath];
END
");

            // drop Speakers.PhotoRelativePath
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'PhotoRelativePath') IS NOT NULL
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'PhotoRelativePath');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Speakers] DROP COLUMN [PhotoRelativePath];
END
");

            // drop SpeakerPresentations.ContentType
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'ContentType') IS NOT NULL
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SpeakerPresentations]') AND [c].[name] = N'ContentType');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [SpeakerPresentations] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [SpeakerPresentations] DROP COLUMN [ContentType];
END
");

            // drop SpeakerPresentations.FileName
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'FileName') IS NOT NULL
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SpeakerPresentations]') AND [c].[name] = N'FileName');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [SpeakerPresentations] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [SpeakerPresentations] DROP COLUMN [FileName];
END
");

            // drop SpeakerPresentations.SizeBytes
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'SizeBytes') IS NOT NULL
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SpeakerPresentations]') AND [c].[name] = N'SizeBytes');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [SpeakerPresentations] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [SpeakerPresentations] DROP COLUMN [SizeBytes];
END
");

            // drop SpeakerPresentations.StoredFileName
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'StoredFileName') IS NOT NULL
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SpeakerPresentations]') AND [c].[name] = N'StoredFileName');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [SpeakerPresentations] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [SpeakerPresentations] DROP COLUMN [StoredFileName];
END
");

            // drop SessionSummaries.SummaryVideoUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'SummaryVideoUrl') IS NOT NULL
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'SummaryVideoUrl');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [SessionSummaries] DROP COLUMN [SummaryVideoUrl];
END
");

            // drop Sessions.LiveSignLanguageUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveSignLanguageUrl') IS NOT NULL
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'LiveSignLanguageUrl');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [LiveSignLanguageUrl];
END
");

            // drop Sessions.LiveStreamUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveStreamUrl') IS NOT NULL
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'LiveStreamUrl');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [LiveStreamUrl];
END
");

            // drop Sessions.RatingPromptSent
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RatingPromptSent') IS NOT NULL
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RatingPromptSent');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RatingPromptSent];
END
");

            // drop Sessions.RecordingContentType
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingContentType') IS NOT NULL
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingContentType');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingContentType];
END
");

            // drop Sessions.RecordingFileName
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingFileName') IS NOT NULL
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingFileName');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingFileName];
END
");

            // drop Sessions.RecordingSizeBytes
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingSizeBytes') IS NOT NULL
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingSizeBytes');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingSizeBytes];
END
");

            // drop Sessions.RecordingStoredFileName
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingStoredFileName') IS NOT NULL
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingStoredFileName');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingStoredFileName];
END
");

            // drop SessionQuestions.IsHidden
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionQuestions]', N'IsHidden') IS NOT NULL
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionQuestions]') AND [c].[name] = N'IsHidden');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [SessionQuestions] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [SessionQuestions] DROP COLUMN [IsHidden];
END
");

            // rename SeatReservations.Expires
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SeatReservations]', N'Expires') IS NOT NULL AND COL_LENGTH(N'[SeatReservations]', N'NoShowReleaseAt') IS NULL
BEGIN
    EXEC sp_rename N'[SeatReservations].[Expires]', N'NoShowReleaseAt', 'COLUMN';
END
");

            // drop SeatReservations.RejectionReason
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SeatReservations]', N'RejectionReason') IS NOT NULL
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SeatReservations]') AND [c].[name] = N'RejectionReason');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [SeatReservations] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [SeatReservations] DROP COLUMN [RejectionReason];
END
");

            // drop ScanIdempotency.ResponseHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ScanIdempotency]', N'ResponseHash') IS NOT NULL
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ScanIdempotency]') AND [c].[name] = N'ResponseHash');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [ScanIdempotency] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [ScanIdempotency] DROP COLUMN [ResponseHash];
END
");

            // drop OrganizationProfile.BackgroundVideoUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'BackgroundVideoUrl') IS NOT NULL
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'BackgroundVideoUrl');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [OrganizationProfile] DROP COLUMN [BackgroundVideoUrl];
END
");

            // drop OrganizationProfile.CurrentYear
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'CurrentYear') IS NOT NULL
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'CurrentYear');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [OrganizationProfile] DROP COLUMN [CurrentYear];
END
");

            // drop OrganizationProfile.LiveStreamUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'LiveStreamUrl') IS NOT NULL
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'LiveStreamUrl');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [OrganizationProfile] DROP COLUMN [LiveStreamUrl];
END
");

            // drop OrganizationProfile.ReleaseDate
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'ReleaseDate') IS NOT NULL
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'ReleaseDate');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [OrganizationProfile] DROP COLUMN [ReleaseDate];
END
");

            // drop OrganizationProfile.VersionDate
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'VersionDate') IS NOT NULL
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'VersionDate');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [OrganizationProfile] DROP COLUMN [VersionDate];
END
");

            // drop News.ImageRelativePath
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'ImageRelativePath') IS NOT NULL
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'ImageRelativePath');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [News] DROP COLUMN [ImageRelativePath];
END
");

            // drop MediaItems.Url
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaItems]', N'Url') IS NOT NULL
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaItems]') AND [c].[name] = N'Url');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [MediaItems] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [MediaItems] DROP COLUMN [Url];
END
");

            // drop HallAttendances.HallId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[HallAttendances]', N'HallId') IS NOT NULL
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[HallAttendances]') AND [c].[name] = N'HallId');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [HallAttendances] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [HallAttendances] DROP COLUMN [HallId];
END
");

            // drop EventEdition.OpenedByUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[EventEdition]', N'OpenedByUserId') IS NOT NULL
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EventEdition]') AND [c].[name] = N'OpenedByUserId');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [EventEdition] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [EventEdition] DROP COLUMN [OpenedByUserId];
END
");

            // drop DelegationMeetingRequests.AvailabilityWindowId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[DelegationMeetingRequests]', N'AvailabilityWindowId') IS NOT NULL
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DelegationMeetingRequests]') AND [c].[name] = N'AvailabilityWindowId');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [DelegationMeetingRequests] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [DelegationMeetingRequests] DROP COLUMN [AvailabilityWindowId];
END
");

            // drop Banners.ImageUrl
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Banners]', N'ImageUrl') IS NOT NULL
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'ImageUrl');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [Banners] DROP COLUMN [ImageUrl];
END
");

            // drop ArchivePastSpeakers.PhotoRelativePath
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchivePastSpeakers]', N'PhotoRelativePath') IS NOT NULL
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ArchivePastSpeakers]') AND [c].[name] = N'PhotoRelativePath');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [ArchivePastSpeakers] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [ArchivePastSpeakers] DROP COLUMN [PhotoRelativePath];
END
");

            // drop ArchiveMediaItems.Url
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchiveMediaItems]', N'Url') IS NOT NULL
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ArchiveMediaItems]') AND [c].[name] = N'Url');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [ArchiveMediaItems] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [ArchiveMediaItems] DROP COLUMN [Url];
END
");

            // drop ArchiveEditions.CoverImageRelativePath
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchiveEditions]', N'CoverImageRelativePath') IS NOT NULL
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ArchiveEditions]') AND [c].[name] = N'CoverImageRelativePath');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [ArchiveEditions] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [ArchiveEditions] DROP COLUMN [CoverImageRelativePath];
END
");

            // add UserProfiles.MobileNumber
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'MobileNumber') IS NULL
BEGIN
    ALTER TABLE [UserProfiles] ADD [MobileNumber] nvarchar(256) NULL;
END
");

            // rename StoredFiles.SecureDestroyed
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StoredFiles]', N'SecureDestroyed') IS NOT NULL AND COL_LENGTH(N'[StoredFiles]', N'SecureDestroyedAt') IS NULL
BEGIN
    EXEC sp_rename N'[StoredFiles].[SecureDestroyed]', N'SecureDestroyedAt', 'COLUMN';
END
");

            // add SpeakerPresentations.StoredFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'StoredFileId') IS NULL
BEGIN
    ALTER TABLE [SpeakerPresentations] ADD [StoredFileId] uniqueidentifier NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
END
");

            // drop SpeakerPresentations.UploadedByUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerPresentations]', N'UploadedByUserId') IS NOT NULL
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SpeakerPresentations]') AND [c].[name] = N'UploadedByUserId');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [SpeakerPresentations] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [SpeakerPresentations] DROP COLUMN [UploadedByUserId];
END
");

            // rename SpeakerMeetingRequests.ReminderSent
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SpeakerMeetingRequests]', N'ReminderSent') IS NOT NULL AND COL_LENGTH(N'[SpeakerMeetingRequests]', N'ReminderSentAt') IS NULL
BEGIN
    EXEC sp_rename N'[SpeakerMeetingRequests].[ReminderSent]', N'ReminderSentAt', 'COLUMN';
END
");

            // rename Sessions.ReminderSent
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'ReminderSent') IS NOT NULL AND COL_LENGTH(N'[Sessions]', N'ReminderSentAt') IS NULL
BEGIN
    EXEC sp_rename N'[Sessions].[ReminderSent]', N'ReminderSentAt', 'COLUMN';
END
");

            // add Sessions.RecordingFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingFileId') IS NULL
BEGIN
    ALTER TABLE [Sessions] ADD [RecordingFileId] uniqueidentifier NULL;
END
");

            // drop Sessions.RecordingUploadedByUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingUploadedByUserId') IS NOT NULL
BEGIN
    DECLARE @var32 nvarchar(max);
    SELECT @var32 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingUploadedByUserId');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var32 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingUploadedByUserId];
END
");

            // add Sessions.RatingPromptSentAt
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RatingPromptSentAt') IS NULL
BEGIN
    ALTER TABLE [Sessions] ADD [RatingPromptSentAt] datetime2 NULL;
END
");

            // drop Sessions.RecordingUploadedAt
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'RecordingUploadedAt') IS NOT NULL
BEGIN
    DECLARE @var33 nvarchar(max);
    SELECT @var33 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'RecordingUploadedAt');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var33 + ';');
    ALTER TABLE [Sessions] DROP COLUMN [RecordingUploadedAt];
END
");

            // rename SeatReservations.ReviewedByUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SeatReservations]', N'ReviewedByUserId') IS NOT NULL AND COL_LENGTH(N'[SeatReservations]', N'ReleasedByUserId') IS NULL
BEGIN
    EXEC sp_rename N'[SeatReservations].[ReviewedByUserId]', N'ReleasedByUserId', 'COLUMN';
END
");

            // drop SeatReservations.ReviewedAt
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SeatReservations]', N'ReviewedAt') IS NOT NULL
BEGIN
    DECLARE @var34 nvarchar(max);
    SELECT @var34 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SeatReservations]') AND [c].[name] = N'ReviewedAt');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [SeatReservations] DROP CONSTRAINT ' + @var34 + ';');
    ALTER TABLE [SeatReservations] DROP COLUMN [ReviewedAt];
END
");

            // rename ProfileTypes.AllowsVipMeetingSlots
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ProfileTypes]', N'AllowsVipMeetingSlots') IS NOT NULL AND COL_LENGTH(N'[ProfileTypes]', N'IsVipTier') IS NULL
BEGIN
    EXEC sp_rename N'[ProfileTypes].[AllowsVipMeetingSlots]', N'IsVipTier', 'COLUMN';
END
");

            // add MediaItems.VideoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaItems]', N'VideoFileId') IS NULL
BEGIN
    ALTER TABLE [MediaItems] ADD [VideoFileId] uniqueidentifier NULL;
END
");

            // drop MediaItems.ThumbnailFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaItems]', N'ThumbnailFileId') IS NOT NULL
BEGIN
    DECLARE @var35 nvarchar(max);
    SELECT @var35 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaItems]') AND [c].[name] = N'ThumbnailFileId');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [MediaItems] DROP CONSTRAINT ' + @var35 + ';');
    ALTER TABLE [MediaItems] DROP COLUMN [ThumbnailFileId];
END
");

            // rename Halls.EquipmentNotes
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Halls]', N'EquipmentNotes') IS NOT NULL AND COL_LENGTH(N'[Halls]', N'FacilityNotes') IS NULL
BEGIN
    EXEC sp_rename N'[Halls].[EquipmentNotes]', N'FacilityNotes', 'COLUMN';
END
");

            // rename GateAssignments.CreateBy
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[GateAssignments]', N'CreateBy') IS NOT NULL AND COL_LENGTH(N'[GateAssignments]', N'CreatedBy') IS NULL
BEGIN
    EXEC sp_rename N'[GateAssignments].[CreateBy]', N'CreatedBy', 'COLUMN';
END
");

            // rename DelegationMeetingRequests.ReminderSent
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[DelegationMeetingRequests]', N'ReminderSent') IS NOT NULL AND COL_LENGTH(N'[DelegationMeetingRequests]', N'ReminderSentAt') IS NULL
BEGIN
    EXEC sp_rename N'[DelegationMeetingRequests].[ReminderSent]', N'ReminderSentAt', 'COLUMN';
END
");

            // alter VisitorShareTokens.Token
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[VisitorShareTokens]', N'Token') IS NOT NULL
BEGIN
    DECLARE @var36 nvarchar(max);
    SELECT @var36 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[VisitorShareTokens]') AND [c].[name] = N'Token');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [VisitorShareTokens] DROP CONSTRAINT ' + @var36 + ';');
    ALTER TABLE [VisitorShareTokens] ALTER COLUMN [Token] varchar(256) NOT NULL;
END
");

            // add VisitorShareTokens.TokenHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[VisitorShareTokens]', N'TokenHash') IS NULL
BEGIN
    ALTER TABLE [VisitorShareTokens] ADD [TokenHash] varchar(64) NOT NULL DEFAULT '';
END
");

            // alter VenueMapNodes.LabelArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[VenueMapNodes]', N'LabelArabic') IS NOT NULL
BEGIN
    DECLARE @var37 nvarchar(max);
    SELECT @var37 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[VenueMapNodes]') AND [c].[name] = N'LabelArabic');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [VenueMapNodes] DROP CONSTRAINT ' + @var37 + ';');
    ALTER TABLE [VenueMapNodes] ALTER COLUMN [LabelArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter UserProfiles.RejectionReasonArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'RejectionReasonArabic') IS NOT NULL
BEGIN
    DECLARE @var38 nvarchar(max);
    SELECT @var38 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'RejectionReasonArabic');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var38 + ';');
    ALTER TABLE [UserProfiles] ALTER COLUMN [RejectionReasonArabic] nvarchar(500) COLLATE Arabic_CI_AI NULL;
END
");

            // alter UserProfiles.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var39 nvarchar(max);
    SELECT @var39 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'NameArabic');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var39 + ';');
    ALTER TABLE [UserProfiles] ALTER COLUMN [NameArabic] nvarchar(50) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter UserProfiles.JobTitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'JobTitleArabic') IS NOT NULL
BEGIN
    DECLARE @var40 nvarchar(max);
    SELECT @var40 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'JobTitleArabic');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var40 + ';');
    ALTER TABLE [UserProfiles] ALTER COLUMN [JobTitleArabic] nvarchar(100) COLLATE Arabic_CI_AI NULL;
END
");

            // alter UserProfiles.HonorificArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'HonorificArabic') IS NOT NULL
BEGIN
    DECLARE @var41 nvarchar(max);
    SELECT @var41 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'HonorificArabic');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var41 + ';');
    ALTER TABLE [UserProfiles] ALTER COLUMN [HonorificArabic] nvarchar(64) COLLATE Arabic_CI_AI NULL;
END
");

            // add UserProfiles.OrganisationOther
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'OrganisationOther') IS NULL
BEGIN
    ALTER TABLE [UserProfiles] ADD [OrganisationOther] nvarchar(150) NULL;
END
");

            // alter Themes.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Themes]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var42 nvarchar(max);
    SELECT @var42 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Themes]') AND [c].[name] = N'NameArabic');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [Themes] DROP CONSTRAINT ' + @var42 + ';');
    ALTER TABLE [Themes] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Themes.DescriptionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Themes]', N'DescriptionArabic') IS NOT NULL
BEGIN
    DECLARE @var43 nvarchar(max);
    SELECT @var43 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Themes]') AND [c].[name] = N'DescriptionArabic');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [Themes] DROP CONSTRAINT ' + @var43 + ';');
    ALTER TABLE [Themes] ALTER COLUMN [DescriptionArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter StoredFiles.Sha256
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StoredFiles]', N'Sha256') IS NOT NULL
BEGIN
    DECLARE @var44 nvarchar(max);
    SELECT @var44 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StoredFiles]') AND [c].[name] = N'Sha256');
    IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [StoredFiles] DROP CONSTRAINT ' + @var44 + ';');
    ALTER TABLE [StoredFiles] ALTER COLUMN [Sha256] char(64) NULL;
END
");

            // add StoredFiles.KekVersion
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[StoredFiles]', N'KekVersion') IS NULL
BEGIN
    ALTER TABLE [StoredFiles] ADD [KekVersion] tinyint NULL;
END
");

            // alter Sponsors.TaglineArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'TaglineArabic') IS NOT NULL
BEGIN
    DECLARE @var45 nvarchar(max);
    SELECT @var45 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sponsors]') AND [c].[name] = N'TaglineArabic');
    IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [Sponsors] DROP CONSTRAINT ' + @var45 + ';');
    ALTER TABLE [Sponsors] ALTER COLUMN [TaglineArabic] nvarchar(256) COLLATE Arabic_CI_AI NULL;
END
");

            // dropindex IX_Sponsors_Tier_NameArabic - see the class summary
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sponsors_Tier_NameArabic' AND object_id = OBJECT_ID(N'[Sponsors]'))
BEGIN
    DROP INDEX [IX_Sponsors_Tier_NameArabic] ON [Sponsors];
END
");

            // alter Sponsors.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var46 nvarchar(max);
    SELECT @var46 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sponsors]') AND [c].[name] = N'NameArabic');
    IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [Sponsors] DROP CONSTRAINT ' + @var46 + ';');
    ALTER TABLE [Sponsors] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Sponsors.CityArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'CityArabic') IS NOT NULL
BEGIN
    DECLARE @var47 nvarchar(max);
    SELECT @var47 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sponsors]') AND [c].[name] = N'CityArabic');
    IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [Sponsors] DROP CONSTRAINT ' + @var47 + ';');
    ALTER TABLE [Sponsors] ALTER COLUMN [CityArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Sponsors.AboutArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'AboutArabic') IS NOT NULL
BEGIN
    DECLARE @var48 nvarchar(max);
    SELECT @var48 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sponsors]') AND [c].[name] = N'AboutArabic');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [Sponsors] DROP CONSTRAINT ' + @var48 + ';');
    ALTER TABLE [Sponsors] ALTER COLUMN [AboutArabic] nvarchar(2048) COLLATE Arabic_CI_AI NULL;
END
");

            // add Sponsors.LogoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sponsors]', N'LogoFileId') IS NULL
BEGIN
    ALTER TABLE [Sponsors] ADD [LogoFileId] uniqueidentifier NULL;
END
");

            // alter Speakers.TrainingExperienceArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'TrainingExperienceArabic') IS NOT NULL
BEGIN
    DECLARE @var49 nvarchar(max);
    SELECT @var49 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'TrainingExperienceArabic');
    IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var49 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [TrainingExperienceArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Speakers.RankArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'RankArabic') IS NOT NULL
BEGIN
    DECLARE @var50 nvarchar(max);
    SELECT @var50 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'RankArabic');
    IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var50 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [RankArabic] nvarchar(256) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Speakers.QualificationsArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'QualificationsArabic') IS NOT NULL
BEGIN
    DECLARE @var51 nvarchar(max);
    SELECT @var51 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'QualificationsArabic');
    IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var51 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [QualificationsArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Speakers.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var52 nvarchar(max);
    SELECT @var52 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'NameArabic');
    IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var52 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Speakers.CityArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'CityArabic') IS NOT NULL
BEGIN
    DECLARE @var53 nvarchar(max);
    SELECT @var53 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'CityArabic');
    IF @var53 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var53 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [CityArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Speakers.BioArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'BioArabic') IS NOT NULL
BEGIN
    DECLARE @var54 nvarchar(max);
    SELECT @var54 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'BioArabic');
    IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var54 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [BioArabic] nvarchar(2048) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Speakers.AwardsArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'AwardsArabic') IS NOT NULL
BEGIN
    DECLARE @var55 nvarchar(max);
    SELECT @var55 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Speakers]') AND [c].[name] = N'AwardsArabic');
    IF @var55 IS NOT NULL EXEC(N'ALTER TABLE [Speakers] DROP CONSTRAINT ' + @var55 + ';');
    ALTER TABLE [Speakers] ALTER COLUMN [AwardsArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // add Speakers.PhotoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Speakers]', N'PhotoFileId') IS NULL
BEGIN
    ALTER TABLE [Speakers] ADD [PhotoFileId] uniqueidentifier NULL;
END
");

            // alter SessionSummaries.SpeakersArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'SpeakersArabic') IS NOT NULL
BEGIN
    DECLARE @var56 nvarchar(max);
    SELECT @var56 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'SpeakersArabic');
    IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var56 + ';');
    ALTER TABLE [SessionSummaries] ALTER COLUMN [SpeakersArabic] nvarchar(1000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SessionSummaries.RecommendationsArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'RecommendationsArabic') IS NOT NULL
BEGIN
    DECLARE @var57 nvarchar(max);
    SELECT @var57 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'RecommendationsArabic');
    IF @var57 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var57 + ';');
    ALTER TABLE [SessionSummaries] ALTER COLUMN [RecommendationsArabic] nvarchar(4000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SessionSummaries.KeyPointsArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'KeyPointsArabic') IS NOT NULL
BEGIN
    DECLARE @var58 nvarchar(max);
    SELECT @var58 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'KeyPointsArabic');
    IF @var58 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var58 + ';');
    ALTER TABLE [SessionSummaries] ALTER COLUMN [KeyPointsArabic] nvarchar(4000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SessionSummaries.FullTextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'FullTextArabic') IS NOT NULL
BEGIN
    DECLARE @var59 nvarchar(max);
    SELECT @var59 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'FullTextArabic');
    IF @var59 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var59 + ';');
    ALTER TABLE [SessionSummaries] ALTER COLUMN [FullTextArabic] nvarchar(max) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SessionSummaries.AiDraftFullTextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'AiDraftFullTextArabic') IS NOT NULL
BEGIN
    DECLARE @var60 nvarchar(max);
    SELECT @var60 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionSummaries]') AND [c].[name] = N'AiDraftFullTextArabic');
    IF @var60 IS NOT NULL EXEC(N'ALTER TABLE [SessionSummaries] DROP CONSTRAINT ' + @var60 + ';');
    ALTER TABLE [SessionSummaries] ALTER COLUMN [AiDraftFullTextArabic] nvarchar(max) COLLATE Arabic_CI_AI NULL;
END
");

            // add SessionSummaries.SummaryVideoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionSummaries]', N'SummaryVideoFileId') IS NULL
BEGIN
    ALTER TABLE [SessionSummaries] ADD [SummaryVideoFileId] uniqueidentifier NULL;
END
");

            // alter Sessions.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var61 nvarchar(max);
    SELECT @var61 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'TitleArabic');
    IF @var61 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var61 + ';');
    ALTER TABLE [Sessions] ALTER COLUMN [TitleArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Sessions.LiveNoticeArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveNoticeArabic') IS NOT NULL
BEGIN
    DECLARE @var62 nvarchar(max);
    SELECT @var62 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'LiveNoticeArabic');
    IF @var62 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var62 + ';');
    ALTER TABLE [Sessions] ALTER COLUMN [LiveNoticeArabic] nvarchar(512) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Sessions.LiveCaptionsArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveCaptionsArabic') IS NOT NULL
BEGIN
    DECLARE @var63 nvarchar(max);
    SELECT @var63 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'LiveCaptionsArabic');
    IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var63 + ';');
    ALTER TABLE [Sessions] ALTER COLUMN [LiveCaptionsArabic] nvarchar(2048) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Sessions.LanguageArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LanguageArabic') IS NOT NULL
BEGIN
    DECLARE @var64 nvarchar(max);
    SELECT @var64 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'LanguageArabic');
    IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var64 + ';');
    ALTER TABLE [Sessions] ALTER COLUMN [LanguageArabic] nvarchar(64) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Sessions.DescriptionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'DescriptionArabic') IS NOT NULL
BEGIN
    DECLARE @var65 nvarchar(max);
    SELECT @var65 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Sessions]') AND [c].[name] = N'DescriptionArabic');
    IF @var65 IS NOT NULL EXEC(N'ALTER TABLE [Sessions] DROP CONSTRAINT ' + @var65 + ';');
    ALTER TABLE [Sessions] ALTER COLUMN [DescriptionArabic] nvarchar(2048) COLLATE Arabic_CI_AI NULL;
END
");

            // add Sessions.LiveSignLanguageFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveSignLanguageFileId') IS NULL
BEGIN
    ALTER TABLE [Sessions] ADD [LiveSignLanguageFileId] uniqueidentifier NULL;
END
");

            // add Sessions.LiveStreamFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Sessions]', N'LiveStreamFileId') IS NULL
BEGIN
    ALTER TABLE [Sessions] ADD [LiveStreamFileId] uniqueidentifier NULL;
END
");

            // alter SessionOutcomes.TextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionOutcomes]', N'TextArabic') IS NOT NULL
BEGIN
    DECLARE @var66 nvarchar(max);
    SELECT @var66 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionOutcomes]') AND [c].[name] = N'TextArabic');
    IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [SessionOutcomes] DROP CONSTRAINT ' + @var66 + ';');
    ALTER TABLE [SessionOutcomes] ALTER COLUMN [TextArabic] nvarchar(512) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SessionCategories.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SessionCategories]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var67 nvarchar(max);
    SELECT @var67 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SessionCategories]') AND [c].[name] = N'NameArabic');
    IF @var67 IS NOT NULL EXEC(N'ALTER TABLE [SessionCategories] DROP CONSTRAINT ' + @var67 + ';');
    ALTER TABLE [SessionCategories] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter SeatReservations.GuestHintArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[SeatReservations]', N'GuestHintArabic') IS NOT NULL
BEGIN
    DECLARE @var68 nvarchar(max);
    SELECT @var68 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SeatReservations]') AND [c].[name] = N'GuestHintArabic');
    IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [SeatReservations] DROP CONSTRAINT ' + @var68 + ';');
    ALTER TABLE [SeatReservations] ALTER COLUMN [GuestHintArabic] nvarchar(256) COLLATE Arabic_CI_AI NULL;
END
");

            // alter ScanIdempotency.RequestHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ScanIdempotency]', N'RequestHash') IS NOT NULL
BEGIN
    DECLARE @var69 nvarchar(max);
    SELECT @var69 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ScanIdempotency]') AND [c].[name] = N'RequestHash');
    IF @var69 IS NOT NULL EXEC(N'ALTER TABLE [ScanIdempotency] DROP CONSTRAINT ' + @var69 + ';');
    ALTER TABLE [ScanIdempotency] ALTER COLUMN [RequestHash] varchar(64) NOT NULL;
END
");

            // alter Regions.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Regions]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var70 nvarchar(max);
    SELECT @var70 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Regions]') AND [c].[name] = N'NameArabic');
    IF @var70 IS NOT NULL EXEC(N'ALTER TABLE [Regions] DROP CONSTRAINT ' + @var70 + ';');
    ALTER TABLE [Regions] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter RatingTypes.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[RatingTypes]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var71 nvarchar(max);
    SELECT @var71 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RatingTypes]') AND [c].[name] = N'NameArabic');
    IF @var71 IS NOT NULL EXEC(N'ALTER TABLE [RatingTypes] DROP CONSTRAINT ' + @var71 + ';');
    ALTER TABLE [RatingTypes] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter RatingTypes.CommentLabelArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[RatingTypes]', N'CommentLabelArabic') IS NOT NULL
BEGIN
    DECLARE @var72 nvarchar(max);
    SELECT @var72 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RatingTypes]') AND [c].[name] = N'CommentLabelArabic');
    IF @var72 IS NOT NULL EXEC(N'ALTER TABLE [RatingTypes] DROP CONSTRAINT ' + @var72 + ';');
    ALTER TABLE [RatingTypes] ALTER COLUMN [CommentLabelArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // alter RatingQuestions.TextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[RatingQuestions]', N'TextArabic') IS NOT NULL
BEGIN
    DECLARE @var73 nvarchar(max);
    SELECT @var73 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RatingQuestions]') AND [c].[name] = N'TextArabic');
    IF @var73 IS NOT NULL EXEC(N'ALTER TABLE [RatingQuestions] DROP CONSTRAINT ' + @var73 + ';');
    ALTER TABLE [RatingQuestions] ALTER COLUMN [TextArabic] nvarchar(512) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter RatingQuestionGroups.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[RatingQuestionGroups]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var74 nvarchar(max);
    SELECT @var74 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RatingQuestionGroups]') AND [c].[name] = N'NameArabic');
    IF @var74 IS NOT NULL EXEC(N'ALTER TABLE [RatingQuestionGroups] DROP CONSTRAINT ' + @var74 + ';');
    ALTER TABLE [RatingQuestionGroups] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter ProgrammeDays.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ProgrammeDays]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var75 nvarchar(max);
    SELECT @var75 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProgrammeDays]') AND [c].[name] = N'TitleArabic');
    IF @var75 IS NOT NULL EXEC(N'ALTER TABLE [ProgrammeDays] DROP CONSTRAINT ' + @var75 + ';');
    ALTER TABLE [ProgrammeDays] ALTER COLUMN [TitleArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // add ProgrammeDays.ImageFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ProgrammeDays]', N'ImageFileId') IS NULL
BEGIN
    ALTER TABLE [ProgrammeDays] ADD [ImageFileId] uniqueidentifier NULL;
END
");

            // alter ProfileTypes.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ProfileTypes]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var76 nvarchar(max);
    SELECT @var76 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProfileTypes]') AND [c].[name] = N'NameArabic');
    IF @var76 IS NOT NULL EXEC(N'ALTER TABLE [ProfileTypes] DROP CONSTRAINT ' + @var76 + ';');
    ALTER TABLE [ProfileTypes] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter OrganizationProfile.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var77 nvarchar(max);
    SELECT @var77 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'TitleArabic');
    IF @var77 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var77 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [TitleArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter OrganizationProfile.SloganArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'SloganArabic') IS NOT NULL
BEGIN
    DECLARE @var78 nvarchar(max);
    SELECT @var78 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'SloganArabic');
    IF @var78 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var78 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [SloganArabic] nvarchar(512) COLLATE Arabic_CI_AI NULL;
END
");

            // alter OrganizationProfile.RegistrationSuccessMessageArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'RegistrationSuccessMessageArabic') IS NOT NULL
BEGIN
    DECLARE @var79 nvarchar(max);
    SELECT @var79 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'RegistrationSuccessMessageArabic');
    IF @var79 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var79 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [RegistrationSuccessMessageArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter OrganizationProfile.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var80 nvarchar(max);
    SELECT @var80 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'NameArabic');
    IF @var80 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var80 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter OrganizationProfile.LocationTextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'LocationTextArabic') IS NOT NULL
BEGIN
    DECLARE @var81 nvarchar(max);
    SELECT @var81 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'LocationTextArabic');
    IF @var81 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var81 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [LocationTextArabic] nvarchar(512) COLLATE Arabic_CI_AI NULL;
END
");

            // alter OrganizationProfile.BioArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'BioArabic') IS NOT NULL
BEGIN
    DECLARE @var82 nvarchar(max);
    SELECT @var82 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationProfile]') AND [c].[name] = N'BioArabic');
    IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationProfile] DROP CONSTRAINT ' + @var82 + ';');
    ALTER TABLE [OrganizationProfile] ALTER COLUMN [BioArabic] nvarchar(4000) COLLATE Arabic_CI_AI NULL;
END
");

            // add OrganizationProfile.BackgroundVideoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'BackgroundVideoFileId') IS NULL
BEGIN
    ALTER TABLE [OrganizationProfile] ADD [BackgroundVideoFileId] uniqueidentifier NULL;
END
");

            // add OrganizationProfile.LiveStreamFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'LiveStreamFileId') IS NULL
BEGIN
    ALTER TABLE [OrganizationProfile] ADD [LiveStreamFileId] uniqueidentifier NULL;
END
");

            // add OrganizationProfile.LogoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationProfile]', N'LogoFileId') IS NULL
BEGIN
    ALTER TABLE [OrganizationProfile] ADD [LogoFileId] uniqueidentifier NULL;
END
");

            // alter OrganizationDetails.ValueArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationDetails]', N'ValueArabic') IS NOT NULL
BEGIN
    DECLARE @var83 nvarchar(max);
    SELECT @var83 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationDetails]') AND [c].[name] = N'ValueArabic');
    IF @var83 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationDetails] DROP CONSTRAINT ' + @var83 + ';');
    ALTER TABLE [OrganizationDetails] ALTER COLUMN [ValueArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter OrganizationDetails.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationDetails]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var84 nvarchar(max);
    SELECT @var84 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationDetails]') AND [c].[name] = N'NameArabic');
    IF @var84 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationDetails] DROP CONSTRAINT ' + @var84 + ';');
    ALTER TABLE [OrganizationDetails] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter OrganizationAboutItems.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationAboutItems]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var85 nvarchar(max);
    SELECT @var85 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationAboutItems]') AND [c].[name] = N'TitleArabic');
    IF @var85 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationAboutItems] DROP CONSTRAINT ' + @var85 + ';');
    ALTER TABLE [OrganizationAboutItems] ALTER COLUMN [TitleArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter OrganizationAboutItems.TextArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[OrganizationAboutItems]', N'TextArabic') IS NOT NULL
BEGIN
    DECLARE @var86 nvarchar(max);
    SELECT @var86 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationAboutItems]') AND [c].[name] = N'TextArabic');
    IF @var86 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationAboutItems] DROP CONSTRAINT ' + @var86 + ';');
    ALTER TABLE [OrganizationAboutItems] ALTER COLUMN [TextArabic] nvarchar(4000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Organisations.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Organisations]', N'NameArabic') IS NOT NULL
BEGIN
    DROP INDEX [IX_Organisations_IsActive_NameArabic] ON [Organisations];
    DECLARE @var87 nvarchar(max);
    SELECT @var87 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Organisations]') AND [c].[name] = N'NameArabic');
    IF @var87 IS NOT NULL EXEC(N'ALTER TABLE [Organisations] DROP CONSTRAINT ' + @var87 + ';');
    ALTER TABLE [Organisations] ALTER COLUMN [NameArabic] nvarchar(150) COLLATE Arabic_CI_AI NOT NULL;
    CREATE INDEX [IX_Organisations_IsActive_NameArabic] ON [Organisations] ([IsActive], [NameArabic]);
END
");

            // alter Organisations.CommercialRegistration
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Organisations]', N'CommercialRegistration') IS NOT NULL
BEGIN
    DROP INDEX [IX_Organisations_CommercialRegistration] ON [Organisations];
    DECLARE @var88 nvarchar(max);
    SELECT @var88 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Organisations]') AND [c].[name] = N'CommercialRegistration');
    IF @var88 IS NOT NULL EXEC(N'ALTER TABLE [Organisations] DROP CONSTRAINT ' + @var88 + ';');
    ALTER TABLE [Organisations] ALTER COLUMN [CommercialRegistration] nvarchar(700) NULL;
    CREATE UNIQUE INDEX [IX_Organisations_CommercialRegistration] ON [Organisations] ([CommercialRegistration]) WHERE [CommercialRegistration] IS NOT NULL;
END
");

            // alter NotificationBroadcasts.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[NotificationBroadcasts]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var89 nvarchar(max);
    SELECT @var89 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NotificationBroadcasts]') AND [c].[name] = N'TitleArabic');
    IF @var89 IS NOT NULL EXEC(N'ALTER TABLE [NotificationBroadcasts] DROP CONSTRAINT ' + @var89 + ';');
    ALTER TABLE [NotificationBroadcasts] ALTER COLUMN [TitleArabic] nvarchar(200) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter NotificationBroadcasts.BodyArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[NotificationBroadcasts]', N'BodyArabic') IS NOT NULL
BEGIN
    DECLARE @var90 nvarchar(max);
    SELECT @var90 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NotificationBroadcasts]') AND [c].[name] = N'BodyArabic');
    IF @var90 IS NOT NULL EXEC(N'ALTER TABLE [NotificationBroadcasts] DROP CONSTRAINT ' + @var90 + ';');
    ALTER TABLE [NotificationBroadcasts] ALTER COLUMN [BodyArabic] nvarchar(2000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter News.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var91 nvarchar(max);
    SELECT @var91 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'TitleArabic');
    IF @var91 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT ' + @var91 + ';');
    ALTER TABLE [News] ALTER COLUMN [TitleArabic] nvarchar(200) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter News.ExcerptArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'ExcerptArabic') IS NOT NULL
BEGIN
    DECLARE @var92 nvarchar(max);
    SELECT @var92 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'ExcerptArabic');
    IF @var92 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT ' + @var92 + ';');
    ALTER TABLE [News] ALTER COLUMN [ExcerptArabic] nvarchar(500) COLLATE Arabic_CI_AI NULL;
END
");

            // alter News.CategoryArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'CategoryArabic') IS NOT NULL
BEGIN
    DECLARE @var93 nvarchar(max);
    SELECT @var93 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'CategoryArabic');
    IF @var93 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT ' + @var93 + ';');
    ALTER TABLE [News] ALTER COLUMN [CategoryArabic] nvarchar(100) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter News.BodyArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'BodyArabic') IS NOT NULL
BEGIN
    DECLARE @var94 nvarchar(max);
    SELECT @var94 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[News]') AND [c].[name] = N'BodyArabic');
    IF @var94 IS NOT NULL EXEC(N'ALTER TABLE [News] DROP CONSTRAINT ' + @var94 + ';');
    ALTER TABLE [News] ALTER COLUMN [BodyArabic] nvarchar(max) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // add News.ImageFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[News]', N'ImageFileId') IS NULL
BEGIN
    ALTER TABLE [News] ADD [ImageFileId] uniqueidentifier NULL;
END
");

            // alter MeetingActionTokens.TokenHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MeetingActionTokens]', N'TokenHash') IS NOT NULL
BEGIN
    DROP INDEX [IX_MeetingActionTokens_TokenHash] ON [MeetingActionTokens];
    DECLARE @var95 nvarchar(max);
    SELECT @var95 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MeetingActionTokens]') AND [c].[name] = N'TokenHash');
    IF @var95 IS NOT NULL EXEC(N'ALTER TABLE [MeetingActionTokens] DROP CONSTRAINT ' + @var95 + ';');
    ALTER TABLE [MeetingActionTokens] ALTER COLUMN [TokenHash] char(64) NOT NULL;
    CREATE UNIQUE INDEX [IX_MeetingActionTokens_TokenHash] ON [MeetingActionTokens] ([TokenHash]);
END
");

            // alter MediaPartners.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaPartners]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var96 nvarchar(max);
    SELECT @var96 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaPartners]') AND [c].[name] = N'NameArabic');
    IF @var96 IS NOT NULL EXEC(N'ALTER TABLE [MediaPartners] DROP CONSTRAINT ' + @var96 + ';');
    ALTER TABLE [MediaPartners] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter MediaPartners.CityArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaPartners]', N'CityArabic') IS NOT NULL
BEGIN
    DECLARE @var97 nvarchar(max);
    SELECT @var97 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaPartners]') AND [c].[name] = N'CityArabic');
    IF @var97 IS NOT NULL EXEC(N'ALTER TABLE [MediaPartners] DROP CONSTRAINT ' + @var97 + ';');
    ALTER TABLE [MediaPartners] ALTER COLUMN [CityArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // add MediaPartners.LogoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaPartners]', N'LogoFileId') IS NULL
BEGIN
    ALTER TABLE [MediaPartners] ADD [LogoFileId] uniqueidentifier NULL;
END
");

            // alter MediaItems.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaItems]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var98 nvarchar(max);
    SELECT @var98 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaItems]') AND [c].[name] = N'TitleArabic');
    IF @var98 IS NOT NULL EXEC(N'ALTER TABLE [MediaItems] DROP CONSTRAINT ' + @var98 + ';');
    ALTER TABLE [MediaItems] ALTER COLUMN [TitleArabic] nvarchar(200) COLLATE Arabic_CI_AI NULL;
END
");

            // alter MediaItems.AlbumArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[MediaItems]', N'AlbumArabic') IS NOT NULL
BEGIN
    DECLARE @var99 nvarchar(max);
    SELECT @var99 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MediaItems]') AND [c].[name] = N'AlbumArabic');
    IF @var99 IS NOT NULL EXEC(N'ALTER TABLE [MediaItems] DROP CONSTRAINT ' + @var99 + ';');
    ALTER TABLE [MediaItems] ALTER COLUMN [AlbumArabic] nvarchar(200) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Interests.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Interests]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var100 nvarchar(max);
    SELECT @var100 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Interests]') AND [c].[name] = N'NameArabic');
    IF @var100 IS NOT NULL EXEC(N'ALTER TABLE [Interests] DROP CONSTRAINT ' + @var100 + ';');
    ALTER TABLE [Interests] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Halls.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Halls]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var101 nvarchar(max);
    SELECT @var101 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Halls]') AND [c].[name] = N'NameArabic');
    IF @var101 IS NOT NULL EXEC(N'ALTER TABLE [Halls] DROP CONSTRAINT ' + @var101 + ';');
    ALTER TABLE [Halls] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Gates.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Gates]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var102 nvarchar(max);
    SELECT @var102 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Gates]') AND [c].[name] = N'NameArabic');
    IF @var102 IS NOT NULL EXEC(N'ALTER TABLE [Gates] DROP CONSTRAINT ' + @var102 + ';');
    ALTER TABLE [Gates] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Gates.DescriptionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Gates]', N'DescriptionArabic') IS NOT NULL
BEGIN
    DECLARE @var103 nvarchar(max);
    SELECT @var103 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Gates]') AND [c].[name] = N'DescriptionArabic');
    IF @var103 IS NOT NULL EXEC(N'ALTER TABLE [Gates] DROP CONSTRAINT ' + @var103 + ';');
    ALTER TABLE [Gates] ALTER COLUMN [DescriptionArabic] nvarchar(1024) COLLATE Arabic_CI_AI NULL;
END
");

            // alter FaqGroups.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[FaqGroups]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var104 nvarchar(max);
    SELECT @var104 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FaqGroups]') AND [c].[name] = N'NameArabic');
    IF @var104 IS NOT NULL EXEC(N'ALTER TABLE [FaqGroups] DROP CONSTRAINT ' + @var104 + ';');
    ALTER TABLE [FaqGroups] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter FaqEntries.QuestionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[FaqEntries]', N'QuestionArabic') IS NOT NULL
BEGIN
    DECLARE @var105 nvarchar(max);
    SELECT @var105 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FaqEntries]') AND [c].[name] = N'QuestionArabic');
    IF @var105 IS NOT NULL EXEC(N'ALTER TABLE [FaqEntries] DROP CONSTRAINT ' + @var105 + ';');
    ALTER TABLE [FaqEntries] ALTER COLUMN [QuestionArabic] nvarchar(512) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter FaqEntries.AnswerArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[FaqEntries]', N'AnswerArabic') IS NOT NULL
BEGIN
    DECLARE @var106 nvarchar(max);
    SELECT @var106 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FaqEntries]') AND [c].[name] = N'AnswerArabic');
    IF @var106 IS NOT NULL EXEC(N'ALTER TABLE [FaqEntries] DROP CONSTRAINT ' + @var106 + ';');
    ALTER TABLE [FaqEntries] ALTER COLUMN [AnswerArabic] nvarchar(4000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Exhibitors.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Exhibitors]', N'NameArabic') IS NOT NULL
BEGIN
    DROP INDEX [IX_Exhibitors_IsActive_NameArabic] ON [Exhibitors];
    DECLARE @var107 nvarchar(max);
    SELECT @var107 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Exhibitors]') AND [c].[name] = N'NameArabic');
    IF @var107 IS NOT NULL EXEC(N'ALTER TABLE [Exhibitors] DROP CONSTRAINT ' + @var107 + ';');
    ALTER TABLE [Exhibitors] ALTER COLUMN [NameArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
    CREATE INDEX [IX_Exhibitors_IsActive_NameArabic] ON [Exhibitors] ([IsActive], [NameArabic]);
END
");

            // alter Exhibitors.CityArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Exhibitors]', N'CityArabic') IS NOT NULL
BEGIN
    DECLARE @var108 nvarchar(max);
    SELECT @var108 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Exhibitors]') AND [c].[name] = N'CityArabic');
    IF @var108 IS NOT NULL EXEC(N'ALTER TABLE [Exhibitors] DROP CONSTRAINT ' + @var108 + ';');
    ALTER TABLE [Exhibitors] ALTER COLUMN [CityArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // add Exhibitors.LogoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Exhibitors]', N'LogoFileId') IS NULL
BEGIN
    ALTER TABLE [Exhibitors] ADD [LogoFileId] uniqueidentifier NULL;
END
");

            // alter DelegationMeetingActionTokens.TokenHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[DelegationMeetingActionTokens]', N'TokenHash') IS NOT NULL
BEGIN
    DROP INDEX [IX_DelegationMeetingActionTokens_TokenHash] ON [DelegationMeetingActionTokens];
    DECLARE @var109 nvarchar(max);
    SELECT @var109 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DelegationMeetingActionTokens]') AND [c].[name] = N'TokenHash');
    IF @var109 IS NOT NULL EXEC(N'ALTER TABLE [DelegationMeetingActionTokens] DROP CONSTRAINT ' + @var109 + ';');
    ALTER TABLE [DelegationMeetingActionTokens] ALTER COLUMN [TokenHash] char(64) NOT NULL;
    CREATE UNIQUE INDEX [IX_DelegationMeetingActionTokens_TokenHash] ON [DelegationMeetingActionTokens] ([TokenHash]);
END
");

            // alter Countries.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Countries]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var110 nvarchar(max);
    SELECT @var110 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Countries]') AND [c].[name] = N'NameArabic');
    IF @var110 IS NOT NULL EXEC(N'ALTER TABLE [Countries] DROP CONSTRAINT ' + @var110 + ';');
    ALTER TABLE [Countries] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter ContentBlocks.ContentArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ContentBlocks]', N'ContentArabic') IS NOT NULL
BEGIN
    DECLARE @var111 nvarchar(max);
    SELECT @var111 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContentBlocks]') AND [c].[name] = N'ContentArabic');
    IF @var111 IS NOT NULL EXEC(N'ALTER TABLE [ContentBlocks] DROP CONSTRAINT ' + @var111 + ';');
    ALTER TABLE [ContentBlocks] ALTER COLUMN [ContentArabic] nvarchar(max) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // add Connections.PairHighUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Connections]', N'PairHighUserId') IS NULL
BEGIN
    ALTER TABLE [Connections] ADD [PairHighUserId] uniqueidentifier NULL;
END
");

            // add Connections.PairLowUserId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Connections]', N'PairLowUserId') IS NULL
BEGIN
    ALTER TABLE [Connections] ADD [PairLowUserId] uniqueidentifier NULL;
END
");

            // alter Booths.SectorArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'SectorArabic') IS NOT NULL
BEGIN
    DECLARE @var112 nvarchar(max);
    SELECT @var112 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'SectorArabic');
    IF @var112 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var112 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [SectorArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Booths.OfficerNameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'OfficerNameArabic') IS NOT NULL
BEGIN
    DECLARE @var113 nvarchar(max);
    SELECT @var113 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'OfficerNameArabic');
    IF @var113 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var113 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [OfficerNameArabic] nvarchar(256) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Booths.OfficerCityArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'OfficerCityArabic') IS NOT NULL
BEGIN
    DECLARE @var114 nvarchar(max);
    SELECT @var114 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'OfficerCityArabic');
    IF @var114 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var114 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [OfficerCityArabic] nvarchar(128) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Booths.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var115 nvarchar(max);
    SELECT @var115 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'NameArabic');
    IF @var115 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var115 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [NameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Booths.ExhibitorNameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'ExhibitorNameArabic') IS NOT NULL
BEGIN
    DECLARE @var116 nvarchar(max);
    SELECT @var116 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'ExhibitorNameArabic');
    IF @var116 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var116 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [ExhibitorNameArabic] nvarchar(256) COLLATE Arabic_CI_AI NULL;
END
");

            // alter Booths.DescriptionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'DescriptionArabic') IS NOT NULL
BEGIN
    DECLARE @var117 nvarchar(max);
    SELECT @var117 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Booths]') AND [c].[name] = N'DescriptionArabic');
    IF @var117 IS NOT NULL EXEC(N'ALTER TABLE [Booths] DROP CONSTRAINT ' + @var117 + ';');
    ALTER TABLE [Booths] ALTER COLUMN [DescriptionArabic] nvarchar(2048) COLLATE Arabic_CI_AI NULL;
END
");

            // add Booths.LogoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Booths]', N'LogoFileId') IS NULL
BEGIN
    ALTER TABLE [Booths] ADD [LogoFileId] uniqueidentifier NULL;
END
");

            // alter Banners.TitleArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Banners]', N'TitleArabic') IS NOT NULL
BEGIN
    DECLARE @var118 nvarchar(max);
    SELECT @var118 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'TitleArabic');
    IF @var118 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT ' + @var118 + ';');
    ALTER TABLE [Banners] ALTER COLUMN [TitleArabic] nvarchar(256) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter Banners.BodyArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Banners]', N'BodyArabic') IS NOT NULL
BEGIN
    DECLARE @var119 nvarchar(max);
    SELECT @var119 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Banners]') AND [c].[name] = N'BodyArabic');
    IF @var119 IS NOT NULL EXEC(N'ALTER TABLE [Banners] DROP CONSTRAINT ' + @var119 + ';');
    ALTER TABLE [Banners] ALTER COLUMN [BodyArabic] nvarchar(2000) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // add Banners.ImageFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[Banners]', N'ImageFileId') IS NULL
BEGIN
    ALTER TABLE [Banners] ADD [ImageFileId] uniqueidentifier NULL;
END
");

            // alter BadgeBatches.NameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[BadgeBatches]', N'NameArabic') IS NOT NULL
BEGIN
    DECLARE @var120 nvarchar(max);
    SELECT @var120 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BadgeBatches]') AND [c].[name] = N'NameArabic');
    IF @var120 IS NOT NULL EXEC(N'ALTER TABLE [BadgeBatches] DROP CONSTRAINT ' + @var120 + ';');
    ALTER TABLE [BadgeBatches] ALTER COLUMN [NameArabic] nvarchar(200) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // add ArchivePastSpeakers.PhotoFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchivePastSpeakers]', N'PhotoFileId') IS NULL
BEGIN
    ALTER TABLE [ArchivePastSpeakers] ADD [PhotoFileId] uniqueidentifier NULL;
END
");

            // add ArchiveMediaItems.MediaFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchiveMediaItems]', N'MediaFileId') IS NULL
BEGIN
    ALTER TABLE [ArchiveMediaItems] ADD [MediaFileId] uniqueidentifier NULL;
END
");

            // add ArchiveEditions.CoverImageFileId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[ArchiveEditions]', N'CoverImageFileId') IS NULL
BEGIN
    ALTER TABLE [ArchiveEditions] ADD [CoverImageFileId] uniqueidentifier NULL;
END
");

            // alter AiPrompts.DisplayNameArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[AiPrompts]', N'DisplayNameArabic') IS NOT NULL
BEGIN
    DECLARE @var121 nvarchar(max);
    SELECT @var121 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiPrompts]') AND [c].[name] = N'DisplayNameArabic');
    IF @var121 IS NOT NULL EXEC(N'ALTER TABLE [AiPrompts] DROP CONSTRAINT ' + @var121 + ';');
    ALTER TABLE [AiPrompts] ALTER COLUMN [DisplayNameArabic] nvarchar(128) COLLATE Arabic_CI_AI NOT NULL;
END
");

            // alter AiPrompts.DescriptionArabic
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[AiPrompts]', N'DescriptionArabic') IS NOT NULL
BEGIN
    DECLARE @var122 nvarchar(max);
    SELECT @var122 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiPrompts]') AND [c].[name] = N'DescriptionArabic');
    IF @var122 IS NOT NULL EXEC(N'ALTER TABLE [AiPrompts] DROP CONSTRAINT ' + @var122 + ';');
    ALTER TABLE [AiPrompts] ALTER COLUMN [DescriptionArabic] nvarchar(512) COLLATE Arabic_CI_AI NULL;
END
");

            // alter AiChatMessages.Content
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[AiChatMessages]', N'Content') IS NOT NULL
BEGIN
    DECLARE @var123 nvarchar(max);
    SELECT @var123 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AiChatMessages]') AND [c].[name] = N'Content');
    IF @var123 IS NOT NULL EXEC(N'ALTER TABLE [AiChatMessages] DROP CONSTRAINT ' + @var123 + ';');
    ALTER TABLE [AiChatMessages] ALTER COLUMN [Content] nvarchar(max) NOT NULL;
END
");

            // createtable BadgeBatchItems
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[BadgeBatchItems]') IS NULL
BEGIN
    CREATE TABLE [BadgeBatchItems] (
        [Id] uniqueidentifier NOT NULL,
        [BadgeBatchId] uniqueidentifier NOT NULL,
        [ProfileTypeId] uniqueidentifier NOT NULL,
        [Count] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BadgeBatchItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BadgeBatchItems_BadgeBatches_BadgeBatchId] FOREIGN KEY ([BadgeBatchId]) REFERENCES [BadgeBatches] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_BadgeBatchItems_ProfileTypes_ProfileTypeId] FOREIGN KEY ([ProfileTypeId]) REFERENCES [ProfileTypes] ([Id]) ON DELETE NO ACTION
    );
END
");

            // createtable ProfileIdentityDocuments
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ProfileIdentityDocuments]') IS NULL
BEGIN
    CREATE TABLE [ProfileIdentityDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [ProfileId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(32) NOT NULL,
        [Number] nvarchar(256) NOT NULL,
        [NumberHash] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ProfileIdentityDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProfileIdentityDocuments_UserProfiles_ProfileId] FOREIGN KEY ([ProfileId]) REFERENCES [UserProfiles] ([Id]) ON DELETE CASCADE
    );
END
");

            // seed Organisations
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Organisations] WHERE [Id] = 'a17e9c42-0b6d-4f58-9e31-7c2a8d5f60b4')
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'City', N'CommercialRegistration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'Email', N'IsActive', N'Name', N'NameArabic', N'Phone', N'Sector', N'UpdatedAt', N'UpdatedBy', N'Website') AND [object_id] = OBJECT_ID(N'[Organisations]'))
        SET IDENTITY_INSERT [Organisations] ON;
    INSERT INTO [Organisations] ([Id], [City], [CommercialRegistration], [CreatedAt], [CreatedBy], [DeletedAt], [Email], [IsActive], [Name], [NameArabic], [Phone], [Sector], [UpdatedAt], [UpdatedBy], [Website])
    VALUES ('a17e9c42-0b6d-4f58-9e31-7c2a8d5f60b4', NULL, NULL, '2026-01-01T00:00:00.0000000', '00000000-0000-0000-0000-000000000000', NULL, NULL, CAST(1 AS bit), N'Other', N'أخرى', NULL, NULL, NULL, NULL, NULL);
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'City', N'CommercialRegistration', N'CreatedAt', N'CreatedBy', N'DeletedAt', N'Email', N'IsActive', N'Name', N'NameArabic', N'Phone', N'Sector', N'UpdatedAt', N'UpdatedBy', N'Website') AND [object_id] = OBJECT_ID(N'[Organisations]'))
        SET IDENTITY_INSERT [Organisations] OFF;
END
");

            // passthrough
            migrationBuilder.Sql(@"
UPDATE [OrganizationProfile] SET [BackgroundVideoFileId] = NULL, [EventEndDate] = '2026-11-25T00:00:00.0000000', [EventStartDate] = '2026-11-23T00:00:00.0000000', [LiveStreamFileId] = NULL, [LogoFileId] = NULL
WHERE [Id] = '00000000-0000-0000-0000-000000000003';
SELECT @@ROWCOUNT;
");

            // createindex IX_VisitorShareTokens_TokenHash
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_VisitorShareTokens_TokenHash' AND object_id = OBJECT_ID(N'[VisitorShareTokens]'))
BEGIN
    CREATE UNIQUE INDEX [IX_VisitorShareTokens_TokenHash] ON [VisitorShareTokens] ([TokenHash]);
END
");

            // addconstraint CK_VisitorShareTokens_RevocationPin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_VisitorShareTokens_RevocationPin' AND parent_object_id = OBJECT_ID(N'[VisitorShareTokens]'))
BEGIN
    ALTER TABLE [VisitorShareTokens] ADD CONSTRAINT [CK_VisitorShareTokens_RevocationPin] CHECK (([IsActive] = 1 AND [RevokedAt] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL));
END
");

            // addconstraint CK_UserProfiles_AccessibilityTextSize
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_UserProfiles_AccessibilityTextSize' AND parent_object_id = OBJECT_ID(N'[UserProfiles]'))
BEGIN
    ALTER TABLE [UserProfiles] ADD CONSTRAINT [CK_UserProfiles_AccessibilityTextSize] CHECK ([AccessibilityTextSize] IN ('small', 'normal', 'large', 'extraLarge'));
END
");

            // addconstraint CK_Themes_DisplayOrder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Themes_DisplayOrder' AND parent_object_id = OBJECT_ID(N'[Themes]'))
BEGIN
    ALTER TABLE [Themes] ADD CONSTRAINT [CK_Themes_DisplayOrder] CHECK ([DisplayOrder] >= 0);
END
");

            // createindex IX_StoredFiles_KekVersion
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoredFiles_KekVersion' AND object_id = OBJECT_ID(N'[StoredFiles]'))
BEGIN
    CREATE INDEX [IX_StoredFiles_KekVersion] ON [StoredFiles] ([KekVersion]) WHERE [IsEncrypted] = 1;
END
");

            // createindex IX_StoredFiles_Service_OwnerEntityId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StoredFiles_Service_OwnerEntityId' AND object_id = OBJECT_ID(N'[StoredFiles]'))
BEGIN
    CREATE UNIQUE INDEX [IX_StoredFiles_Service_OwnerEntityId] ON [StoredFiles] ([Service], [OwnerEntityId]) WHERE [IsActive] = 1 AND [OwnerEntityId] IS NOT NULL AND [Service] IN (0, 4, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 23, 24);
END
");

            // addconstraint CK_StoredFiles_SizeBytes
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_StoredFiles_SizeBytes' AND parent_object_id = OBJECT_ID(N'[StoredFiles]'))
BEGIN
    ALTER TABLE [StoredFiles] ADD CONSTRAINT [CK_StoredFiles_SizeBytes] CHECK ([SizeBytes] IS NULL OR [SizeBytes] > 0);
END
");

            // createindex IX_Sponsors_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sponsors_LogoFileId' AND object_id = OBJECT_ID(N'[Sponsors]'))
BEGIN
    CREATE INDEX [IX_Sponsors_LogoFileId] ON [Sponsors] ([LogoFileId]);
END
");

            // createindex IX_Sponsors_Tier_NameArabic
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sponsors_Tier_NameArabic' AND object_id = OBJECT_ID(N'[Sponsors]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Sponsors_Tier_NameArabic] ON [Sponsors] ([Tier], [NameArabic]) WHERE [IsActive] = 1;
END
");

            // addconstraint CK_Sponsors_Coordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Sponsors_Coordinates' AND parent_object_id = OBJECT_ID(N'[Sponsors]'))
BEGIN
    ALTER TABLE [Sponsors] ADD CONSTRAINT [CK_Sponsors_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180));
END
");

            // createindex IX_Speakers_PhotoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Speakers_PhotoFileId' AND object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    CREATE INDEX [IX_Speakers_PhotoFileId] ON [Speakers] ([PhotoFileId]);
END
");

            // createindex IX_Speakers_UserProfileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Speakers_UserProfileId' AND object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Speakers_UserProfileId] ON [Speakers] ([UserProfileId]) WHERE [UserProfileId] IS NOT NULL;
END
");

            // addconstraint CK_Speakers_DisplayOrder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Speakers_DisplayOrder' AND parent_object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    ALTER TABLE [Speakers] ADD CONSTRAINT [CK_Speakers_DisplayOrder] CHECK ([DisplayOrder] >= 0);
END
");

            // addconstraint CK_Speakers_Location
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Speakers_Location' AND parent_object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    ALTER TABLE [Speakers] ADD CONSTRAINT [CK_Speakers_Location] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180));
END
");

            // createindex IX_SpeakerPresentations_StoredFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SpeakerPresentations_StoredFileId' AND object_id = OBJECT_ID(N'[SpeakerPresentations]'))
BEGIN
    CREATE INDEX [IX_SpeakerPresentations_StoredFileId] ON [SpeakerPresentations] ([StoredFileId]);
END
");

            // createindex IX_SpeakerMeetingRequests_RequestedByUserId_SpeakerId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SpeakerMeetingRequests_RequestedByUserId_SpeakerId' AND object_id = OBJECT_ID(N'[SpeakerMeetingRequests]'))
BEGIN
    CREATE UNIQUE INDEX [IX_SpeakerMeetingRequests_RequestedByUserId_SpeakerId] ON [SpeakerMeetingRequests] ([RequestedByUserId], [SpeakerId]) WHERE [Status] = 0;
END
");

            // addconstraint CK_SpeakerAvailabilityWindows_SlotMinutes
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SpeakerAvailabilityWindows_SlotMinutes' AND parent_object_id = OBJECT_ID(N'[SpeakerAvailabilityWindows]'))
BEGIN
    ALTER TABLE [SpeakerAvailabilityWindows] ADD CONSTRAINT [CK_SpeakerAvailabilityWindows_SlotMinutes] CHECK ([SlotMinutes] >= 5 AND [SlotMinutes] <= 480);
END
");

            // createindex IX_SessionSummaries_SummaryVideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionSummaries_SummaryVideoFileId' AND object_id = OBJECT_ID(N'[SessionSummaries]'))
BEGIN
    CREATE INDEX [IX_SessionSummaries_SummaryVideoFileId] ON [SessionSummaries] ([SummaryVideoFileId]);
END
");

            // addconstraint CK_SessionSummaries_PublishPin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SessionSummaries_PublishPin' AND parent_object_id = OBJECT_ID(N'[SessionSummaries]'))
BEGIN
    ALTER TABLE [SessionSummaries] ADD CONSTRAINT [CK_SessionSummaries_PublishPin] CHECK (([PublishedAt] IS NULL AND [PublishedByUserId] IS NULL) OR ([PublishedAt] IS NOT NULL AND [PublishedByUserId] IS NOT NULL AND [ApprovedAt] IS NOT NULL));
END
");

            // createindex IX_Sessions_LiveSignLanguageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sessions_LiveSignLanguageFileId' AND object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    CREATE INDEX [IX_Sessions_LiveSignLanguageFileId] ON [Sessions] ([LiveSignLanguageFileId]);
END
");

            // createindex IX_Sessions_LiveStreamFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sessions_LiveStreamFileId' AND object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    CREATE INDEX [IX_Sessions_LiveStreamFileId] ON [Sessions] ([LiveStreamFileId]);
END
");

            // createindex IX_Sessions_RecordingFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Sessions_RecordingFileId' AND object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    CREATE INDEX [IX_Sessions_RecordingFileId] ON [Sessions] ([RecordingFileId]);
END
");

            // addconstraint CK_Sessions_CapacityOverride
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Sessions_CapacityOverride' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [CK_Sessions_CapacityOverride] CHECK ([CapacityOverride] IS NULL OR [CapacityOverride] >= 0);
END
");

            // addconstraint CK_Sessions_PublishedAtPin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Sessions_PublishedAtPin' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [CK_Sessions_PublishedAtPin] CHECK (([Status] = 3 AND [PublishedAt] IS NOT NULL) OR ([Status] <> 3 AND [PublishedAt] IS NULL));
END
");

            // addconstraint CK_Sessions_RecordedHasRecording
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Sessions_RecordedHasRecording' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [CK_Sessions_RecordedHasRecording] CHECK ([Status] NOT IN (2, 3) OR [RecordingFileId] IS NOT NULL);
END
");

            // createindex IX_SessionQuestions_SessionId_IsPushed_Order
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionQuestions_SessionId_IsPushed_Order' AND object_id = OBJECT_ID(N'[SessionQuestions]'))
BEGIN
    CREATE INDEX [IX_SessionQuestions_SessionId_IsPushed_Order] ON [SessionQuestions] ([SessionId], [IsPushed], [Order]);
END
");

            // addconstraint CK_SessionQuestions_EscalationTrio
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SessionQuestions_EscalationTrio' AND parent_object_id = OBJECT_ID(N'[SessionQuestions]'))
BEGIN
    ALTER TABLE [SessionQuestions] ADD CONSTRAINT [CK_SessionQuestions_EscalationTrio] CHECK (([AssignedToRole] IS NULL AND [EscalatedByUserId] IS NULL AND [EscalatedAt] IS NULL) OR ([AssignedToRole] IS NOT NULL AND [EscalatedByUserId] IS NOT NULL AND [EscalatedAt] IS NOT NULL));
END
");

            // addconstraint CK_SessionQuestions_PushedPair
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SessionQuestions_PushedPair' AND parent_object_id = OBJECT_ID(N'[SessionQuestions]'))
BEGIN
    ALTER TABLE [SessionQuestions] ADD CONSTRAINT [CK_SessionQuestions_PushedPair] CHECK (([IsPushed] = 0 AND [PushedAt] IS NULL) OR ([IsPushed] = 1 AND [PushedAt] IS NOT NULL));
END
");

            // createindex IX_SessionOutcomes_SessionId_DisplayOrder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SessionOutcomes_SessionId_DisplayOrder' AND object_id = OBJECT_ID(N'[SessionOutcomes]'))
BEGIN
    CREATE INDEX [IX_SessionOutcomes_SessionId_DisplayOrder] ON [SessionOutcomes] ([SessionId], [DisplayOrder]);
END
");

            // createindex IX_SeatReservations_NoShowReleaseAt
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeatReservations_NoShowReleaseAt' AND object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    CREATE INDEX [IX_SeatReservations_NoShowReleaseAt] ON [SeatReservations] ([NoShowReleaseAt]) WHERE [ReleasedAt] IS NULL AND [NoShowReleaseAt] IS NOT NULL;
END
");

            // addconstraint CK_SeatReservations_AdminBlockHasNoHolder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SeatReservations_AdminBlockHasNoHolder' AND parent_object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    ALTER TABLE [SeatReservations] ADD CONSTRAINT [CK_SeatReservations_AdminBlockHasNoHolder] CHECK (([Kind] = 1 AND [ReservedForProfileId] IS NULL) OR ([Kind] <> 1 AND [ReservedForProfileId] IS NOT NULL));
END
");

            // addconstraint CK_SeatReservations_ReleasePin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SeatReservations_ReleasePin' AND parent_object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    ALTER TABLE [SeatReservations] ADD CONSTRAINT [CK_SeatReservations_ReleasePin] CHECK ([ReleasedAt] IS NULL OR [Status] = 3);
END
");

            // addconstraint CK_SeatReservations_SeatNumber
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SeatReservations_SeatNumber' AND parent_object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    ALTER TABLE [SeatReservations] ADD CONSTRAINT [CK_SeatReservations_SeatNumber] CHECK ([SeatNumber] >= 1);
END
");

            // addconstraint CK_SeatReservations_SeatPair
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SeatReservations_SeatPair' AND parent_object_id = OBJECT_ID(N'[SeatReservations]'))
BEGIN
    ALTER TABLE [SeatReservations] ADD CONSTRAINT [CK_SeatReservations_SeatPair] CHECK (([RowLabel] IS NULL AND [SeatNumber] IS NULL) OR ([RowLabel] IS NOT NULL AND [SeatNumber] IS NOT NULL));
END
");

            // addconstraint CK_SavedContacts_NotSelf
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_SavedContacts_NotSelf' AND parent_object_id = OBJECT_ID(N'[SavedContacts]'))
BEGIN
    ALTER TABLE [SavedContacts] ADD CONSTRAINT [CK_SavedContacts_NotSelf] CHECK ([OwnerUserId] <> [SubjectUserId]);
END
");

            // createindex IX_RatingResponses_TargetId_IsActive
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RatingResponses_TargetId_IsActive' AND object_id = OBJECT_ID(N'[RatingResponses]'))
BEGIN
    CREATE INDEX [IX_RatingResponses_TargetId_IsActive] ON [RatingResponses] ([TargetId], [IsActive]);
END
");

            // createindex IX_ProgrammeDays_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProgrammeDays_ImageFileId' AND object_id = OBJECT_ID(N'[ProgrammeDays]'))
BEGIN
    CREATE INDEX [IX_ProgrammeDays_ImageFileId] ON [ProgrammeDays] ([ImageFileId]);
END
");

            // createindex IX_OrganizationProfile_BackgroundVideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationProfile_BackgroundVideoFileId' AND object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    CREATE INDEX [IX_OrganizationProfile_BackgroundVideoFileId] ON [OrganizationProfile] ([BackgroundVideoFileId]);
END
");

            // createindex IX_OrganizationProfile_LiveStreamFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationProfile_LiveStreamFileId' AND object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    CREATE INDEX [IX_OrganizationProfile_LiveStreamFileId] ON [OrganizationProfile] ([LiveStreamFileId]);
END
");

            // createindex IX_OrganizationProfile_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrganizationProfile_LogoFileId' AND object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    CREATE INDEX [IX_OrganizationProfile_LogoFileId] ON [OrganizationProfile] ([LogoFileId]);
END
");

            // addconstraint CK_OrganizationProfile_Coordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_OrganizationProfile_Coordinates' AND parent_object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    ALTER TABLE [OrganizationProfile] ADD CONSTRAINT [CK_OrganizationProfile_Coordinates] CHECK (([Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)) AND ([Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180)));
END
");

            // addconstraint CK_OrganizationProfile_EventWindow
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_OrganizationProfile_EventWindow' AND parent_object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    ALTER TABLE [OrganizationProfile] ADD CONSTRAINT [CK_OrganizationProfile_EventWindow] CHECK ([EventStartDate] IS NULL OR [EventEndDate] IS NULL OR [EventEndDate] >= [EventStartDate]);
END
");

            // addconstraint CK_NotificationBroadcasts_TargetArc
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_NotificationBroadcasts_TargetArc' AND parent_object_id = OBJECT_ID(N'[NotificationBroadcasts]'))
BEGIN
    ALTER TABLE [NotificationBroadcasts] ADD CONSTRAINT [CK_NotificationBroadcasts_TargetArc] CHECK (([TargetMode] = 'Session' AND [SessionId] IS NOT NULL AND [AudienceScope] IS NULL) OR ([TargetMode] = 'Audience' AND [AudienceScope] IS NOT NULL AND [SessionId] IS NULL));
END
");

            // createindex IX_News_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_News_ImageFileId' AND object_id = OBJECT_ID(N'[News]'))
BEGIN
    CREATE INDEX [IX_News_ImageFileId] ON [News] ([ImageFileId]);
END
");

            // addconstraint CK_MeetingTables_Capacity
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_MeetingTables_Capacity' AND parent_object_id = OBJECT_ID(N'[MeetingTables]'))
BEGIN
    ALTER TABLE [MeetingTables] ADD CONSTRAINT [CK_MeetingTables_Capacity] CHECK ([Capacity] >= 2 AND [Capacity] <= 100);
END
");

            // createindex IX_MediaPartners_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaPartners_LogoFileId' AND object_id = OBJECT_ID(N'[MediaPartners]'))
BEGIN
    CREATE INDEX [IX_MediaPartners_LogoFileId] ON [MediaPartners] ([LogoFileId]);
END
");

            // createindex IX_MediaPartners_Name
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaPartners_Name' AND object_id = OBJECT_ID(N'[MediaPartners]'))
BEGIN
    CREATE UNIQUE INDEX [IX_MediaPartners_Name] ON [MediaPartners] ([Name]);
END
");

            // addconstraint CK_MediaPartners_Coordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_MediaPartners_Coordinates' AND parent_object_id = OBJECT_ID(N'[MediaPartners]'))
BEGIN
    ALTER TABLE [MediaPartners] ADD CONSTRAINT [CK_MediaPartners_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180));
END
");

            // createindex IX_MediaItems_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaItems_ImageFileId' AND object_id = OBJECT_ID(N'[MediaItems]'))
BEGIN
    CREATE INDEX [IX_MediaItems_ImageFileId] ON [MediaItems] ([ImageFileId]);
END
");

            // createindex IX_MediaItems_VideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MediaItems_VideoFileId' AND object_id = OBJECT_ID(N'[MediaItems]'))
BEGIN
    CREATE INDEX [IX_MediaItems_VideoFileId] ON [MediaItems] ([VideoFileId]);
END
");

            // addconstraint CK_Invitations_ResponsePin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Invitations_ResponsePin' AND parent_object_id = OBJECT_ID(N'[Invitations]'))
BEGIN
    ALTER TABLE [Invitations] ADD CONSTRAINT [CK_Invitations_ResponsePin] CHECK (([State] = 0 AND [RespondedAt] IS NULL) OR ([State] <> 0 AND [RespondedAt] IS NOT NULL));
END
");

            // addconstraint CK_HallSeatLayouts_SeatsPerRow
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_HallSeatLayouts_SeatsPerRow' AND parent_object_id = OBJECT_ID(N'[HallSeatLayouts]'))
BEGIN
    ALTER TABLE [HallSeatLayouts] ADD CONSTRAINT [CK_HallSeatLayouts_SeatsPerRow] CHECK ([SeatsPerRow] >= 1 AND [SeatsPerRow] <= 80);
END
");

            // addconstraint CK_Halls_Capacity
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Halls_Capacity' AND parent_object_id = OBJECT_ID(N'[Halls]'))
BEGIN
    ALTER TABLE [Halls] ADD CONSTRAINT [CK_Halls_Capacity] CHECK ([Capacity] >= 0);
END
");

            // addconstraint CK_Halls_Geofence
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Halls_Geofence' AND parent_object_id = OBJECT_ID(N'[Halls]'))
BEGIN
    ALTER TABLE [Halls] ADD CONSTRAINT [CK_Halls_Geofence] CHECK (([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceCenterLat] >= -90 AND [GeofenceCenterLat] <= 90 AND [GeofenceCenterLon] >= -180 AND [GeofenceCenterLon] <= 180 AND [GeofenceRadiusMeters] > 0 AND [GeofenceRadiusMeters] <= 100000));
END
");

            // addconstraint CK_HallAvailabilityWindows_SlotMinutes
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_HallAvailabilityWindows_SlotMinutes' AND parent_object_id = OBJECT_ID(N'[HallAvailabilityWindows]'))
BEGIN
    ALTER TABLE [HallAvailabilityWindows] ADD CONSTRAINT [CK_HallAvailabilityWindows_SlotMinutes] CHECK ([SlotMinutes] >= 5 AND [SlotMinutes] <= 480);
END
");

            // createindex IX_HallAttendances_SessionId_UserProfileId_Leave
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HallAttendances_SessionId_UserProfileId_Leave' AND object_id = OBJECT_ID(N'[HallAttendances]'))
BEGIN
    CREATE INDEX [IX_HallAttendances_SessionId_UserProfileId_Leave] ON [HallAttendances] ([SessionId], [UserProfileId], [Leave]);
END
");

            // addconstraint CK_HallAttendances_LeaveOrder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_HallAttendances_LeaveOrder' AND parent_object_id = OBJECT_ID(N'[HallAttendances]'))
BEGIN
    ALTER TABLE [HallAttendances] ADD CONSTRAINT [CK_HallAttendances_LeaveOrder] CHECK ([Leave] IS NULL OR [Leave] >= [Enter]);
END
");

            // addconstraint CK_HallAllocations_RowColumnSpec
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_HallAllocations_RowColumnSpec' AND parent_object_id = OBJECT_ID(N'[HallAllocations]'))
BEGIN
    ALTER TABLE [HallAllocations] ADD CONSTRAINT [CK_HallAllocations_RowColumnSpec] CHECK (([Mode] = 2 AND [RowColumnSpec] IS NOT NULL) OR ([Mode] <> 2 AND [RowColumnSpec] IS NULL));
END
");

            // addconstraint CK_HallAllocations_UnitCount
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_HallAllocations_UnitCount' AND parent_object_id = OBJECT_ID(N'[HallAllocations]'))
BEGIN
    ALTER TABLE [HallAllocations] ADD CONSTRAINT [CK_HallAllocations_UnitCount] CHECK (([Mode] = 1 AND [UnitCount] >= 1) OR ([Mode] <> 1 AND [UnitCount] IS NULL));
END
");

            // createindex IX_GateScan_UserProfile_ScannedAt
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GateScan_UserProfile_ScannedAt' AND object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    CREATE INDEX [IX_GateScan_UserProfile_ScannedAt] ON [GateScans] ([UserProfileId], [ScannedAt] DESC);
END
");

            // addconstraint CK_GateScans_DenialReasonRange
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_GateScans_DenialReasonRange' AND parent_object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    ALTER TABLE [GateScans] ADD CONSTRAINT [CK_GateScans_DenialReasonRange] CHECK ([DenialReasonCode] IS NULL OR [DenialReasonCode] BETWEEN 0 AND 8);
END
");

            // addconstraint CK_GateScans_DirectionRange
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_GateScans_DirectionRange' AND parent_object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    ALTER TABLE [GateScans] ADD CONSTRAINT [CK_GateScans_DirectionRange] CHECK ([Direction] BETWEEN 0 AND 1);
END
");

            // addconstraint CK_GateScans_SourceRange
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_GateScans_SourceRange' AND parent_object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    ALTER TABLE [GateScans] ADD CONSTRAINT [CK_GateScans_SourceRange] CHECK ([Source] BETWEEN 0 AND 2);
END
");

            // addconstraint CK_Gates_DirectionModeRange
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Gates_DirectionModeRange' AND parent_object_id = OBJECT_ID(N'[Gates]'))
BEGIN
    ALTER TABLE [Gates] ADD CONSTRAINT [CK_Gates_DirectionModeRange] CHECK ([DirectionMode] BETWEEN 0 AND 2);
END
");

            // addconstraint CK_GateAssignments_RevocationPin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_GateAssignments_RevocationPin' AND parent_object_id = OBJECT_ID(N'[GateAssignments]'))
BEGIN
    ALTER TABLE [GateAssignments] ADD CONSTRAINT [CK_GateAssignments_RevocationPin] CHECK (([IsActive] = 1 AND [RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL));
END
");

            // createindex IX_Exhibitors_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Exhibitors_LogoFileId' AND object_id = OBJECT_ID(N'[Exhibitors]'))
BEGIN
    CREATE INDEX [IX_Exhibitors_LogoFileId] ON [Exhibitors] ([LogoFileId]);
END
");

            // addconstraint CK_Exhibitors_Coordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Exhibitors_Coordinates' AND parent_object_id = OBJECT_ID(N'[Exhibitors]'))
BEGIN
    ALTER TABLE [Exhibitors] ADD CONSTRAINT [CK_Exhibitors_Coordinates] CHECK (([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180));
END
");

            // addconstraint CK_EventEdition_Year
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_EventEdition_Year' AND parent_object_id = OBJECT_ID(N'[EventEdition]'))
BEGIN
    ALTER TABLE [EventEdition] ADD CONSTRAINT [CK_EventEdition_Year] CHECK ([Year] BETWEEN 2000 AND 2999);
END
");

            // addconstraint CK_DevicePositionPings_Coordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_DevicePositionPings_Coordinates' AND parent_object_id = OBJECT_ID(N'[DevicePositionPings]'))
BEGIN
    ALTER TABLE [DevicePositionPings] ADD CONSTRAINT [CK_DevicePositionPings_Coordinates] CHECK ([Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180);
END
");

            // createindex IX_DelegationMeetingRequests_RequestedByUserId_TargetCountryId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DelegationMeetingRequests_RequestedByUserId_TargetCountryId' AND object_id = OBJECT_ID(N'[DelegationMeetingRequests]'))
BEGIN
    CREATE UNIQUE INDEX [IX_DelegationMeetingRequests_RequestedByUserId_TargetCountryId] ON [DelegationMeetingRequests] ([RequestedByUserId], [TargetCountryId]) WHERE [Status] = 0;
END
");

            // addconstraint CK_DelegationMeetingRequests_AttendeeCount
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_DelegationMeetingRequests_AttendeeCount' AND parent_object_id = OBJECT_ID(N'[DelegationMeetingRequests]'))
BEGIN
    ALTER TABLE [DelegationMeetingRequests] ADD CONSTRAINT [CK_DelegationMeetingRequests_AttendeeCount] CHECK ([AttendeeCount] >= 1 AND [AttendeeCount] <= 100);
END
");

            // addconstraint CK_DelegationMeetingRequests_NotSelf
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_DelegationMeetingRequests_NotSelf' AND parent_object_id = OBJECT_ID(N'[DelegationMeetingRequests]'))
BEGIN
    ALTER TABLE [DelegationMeetingRequests] ADD CONSTRAINT [CK_DelegationMeetingRequests_NotSelf] CHECK ([RequestingCountryId] <> [TargetCountryId]);
END
");

            // addconstraint CK_DelegationAvailabilityWindows_SlotMinutes
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_DelegationAvailabilityWindows_SlotMinutes' AND parent_object_id = OBJECT_ID(N'[DelegationAvailabilityWindows]'))
BEGIN
    ALTER TABLE [DelegationAvailabilityWindows] ADD CONSTRAINT [CK_DelegationAvailabilityWindows_SlotMinutes] CHECK ([SlotMinutes] >= 5 AND [SlotMinutes] <= 480);
END
");

            // addconstraint CK_Countries_DelegationWindow
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Countries_DelegationWindow' AND parent_object_id = OBJECT_ID(N'[Countries]'))
BEGIN
    ALTER TABLE [Countries] ADD CONSTRAINT [CK_Countries_DelegationWindow] CHECK ([DelegationArrivalDate] IS NULL OR [DelegationDepartureDate] IS NULL OR [DelegationDepartureDate] >= [DelegationArrivalDate]);
END
");

            // addconstraint CK_ContactInquiries_HandledPin
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_ContactInquiries_HandledPin' AND parent_object_id = OBJECT_ID(N'[ContactInquiries]'))
BEGIN
    ALTER TABLE [ContactInquiries] ADD CONSTRAINT [CK_ContactInquiries_HandledPin] CHECK (([IsHandled] = 0 AND [HandledAt] IS NULL AND [HandledByUserId] IS NULL) OR ([IsHandled] = 1 AND [HandledAt] IS NOT NULL AND [HandledByUserId] IS NOT NULL));
END
");

            // createindex IX_Connections_PairLowUserId_PairHighUserId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Connections_PairLowUserId_PairHighUserId' AND object_id = OBJECT_ID(N'[Connections]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Connections_PairLowUserId_PairHighUserId] ON [Connections] ([PairLowUserId], [PairHighUserId]) WHERE [IsActive] = 1 AND [PairLowUserId] IS NOT NULL;
END
");

            // addconstraint CK_Connections_NotSelf
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Connections_NotSelf' AND parent_object_id = OBJECT_ID(N'[Connections]'))
BEGIN
    ALTER TABLE [Connections] ADD CONSTRAINT [CK_Connections_NotSelf] CHECK ([RequesterUserId] <> [TargetUserId]);
END
");

            // createindex IX_Booths_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Booths_LogoFileId' AND object_id = OBJECT_ID(N'[Booths]'))
BEGIN
    CREATE INDEX [IX_Booths_LogoFileId] ON [Booths] ([LogoFileId]);
END
");

            // addconstraint CK_Booths_OfficerCoordinates
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_Booths_OfficerCoordinates' AND parent_object_id = OBJECT_ID(N'[Booths]'))
BEGIN
    ALTER TABLE [Booths] ADD CONSTRAINT [CK_Booths_OfficerCoordinates] CHECK (([OfficerLatitude] IS NULL AND [OfficerLongitude] IS NULL) OR ([OfficerLatitude] IS NOT NULL AND [OfficerLongitude] IS NOT NULL AND [OfficerLatitude] >= -90 AND [OfficerLatitude] <= 90 AND [OfficerLongitude] >= -180 AND [OfficerLongitude] <= 180));
END
");

            // createindex IX_Banners_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Banners_ImageFileId' AND object_id = OBJECT_ID(N'[Banners]'))
BEGIN
    CREATE INDEX [IX_Banners_ImageFileId] ON [Banners] ([ImageFileId]);
END
");

            // createindex IX_ArchivePastSpeakers_PhotoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ArchivePastSpeakers_PhotoFileId' AND object_id = OBJECT_ID(N'[ArchivePastSpeakers]'))
BEGIN
    CREATE INDEX [IX_ArchivePastSpeakers_PhotoFileId] ON [ArchivePastSpeakers] ([PhotoFileId]);
END
");

            // createindex IX_ArchiveMediaItems_MediaFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ArchiveMediaItems_MediaFileId' AND object_id = OBJECT_ID(N'[ArchiveMediaItems]'))
BEGIN
    CREATE INDEX [IX_ArchiveMediaItems_MediaFileId] ON [ArchiveMediaItems] ([MediaFileId]);
END
");

            // createindex IX_ArchiveEditions_CoverImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ArchiveEditions_CoverImageFileId' AND object_id = OBJECT_ID(N'[ArchiveEditions]'))
BEGIN
    CREATE INDEX [IX_ArchiveEditions_CoverImageFileId] ON [ArchiveEditions] ([CoverImageFileId]);
END
");

            // addconstraint CK_ArchiveEditions_CountersNonNegative
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_ArchiveEditions_CountersNonNegative' AND parent_object_id = OBJECT_ID(N'[ArchiveEditions]'))
BEGIN
    ALTER TABLE [ArchiveEditions] ADD CONSTRAINT [CK_ArchiveEditions_CountersNonNegative] CHECK ([Attendees] >= 0 AND [Sessions] >= 0 AND [Speakers] >= 0);
END
");

            // addconstraint CK_ArchiveEditions_YearRange
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_ArchiveEditions_YearRange' AND parent_object_id = OBJECT_ID(N'[ArchiveEditions]'))
BEGIN
    ALTER TABLE [ArchiveEditions] ADD CONSTRAINT [CK_ArchiveEditions_YearRange] CHECK ([Year] >= 2000 AND [Year] <= 2100);
END
");

            // addconstraint CK_AiPrompts_MaxOutputTokens
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_AiPrompts_MaxOutputTokens' AND parent_object_id = OBJECT_ID(N'[AiPrompts]'))
BEGIN
    ALTER TABLE [AiPrompts] ADD CONSTRAINT [CK_AiPrompts_MaxOutputTokens] CHECK ([MaxOutputTokens] >= 1 AND [MaxOutputTokens] <= 8000);
END
");

            // addconstraint CK_AiPrompts_Temperature
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_AiPrompts_Temperature' AND parent_object_id = OBJECT_ID(N'[AiPrompts]'))
BEGIN
    ALTER TABLE [AiPrompts] ADD CONSTRAINT [CK_AiPrompts_Temperature] CHECK ([Temperature] >= 0 AND [Temperature] <= 2);
END
");

            // addconstraint CK_AiInvocations_CallerKind
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_AiInvocations_CallerKind' AND parent_object_id = OBJECT_ID(N'[AiInvocations]'))
BEGIN
    ALTER TABLE [AiInvocations] ADD CONSTRAINT [CK_AiInvocations_CallerKind] CHECK ([CallerKind] IN ('Anonymous', 'Visitor', 'Staff', 'Admin', 'Moderator'));
END
");

            // addconstraint CK_AiChatMessages_Role
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'CK_AiChatMessages_Role' AND parent_object_id = OBJECT_ID(N'[AiChatMessages]'))
BEGIN
    ALTER TABLE [AiChatMessages] ADD CONSTRAINT [CK_AiChatMessages_Role] CHECK ([Role] IN ('user', 'assistant'));
END
");

            // createindex IX_BadgeBatchItems_BadgeBatchId_DisplayOrder
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BadgeBatchItems_BadgeBatchId_DisplayOrder' AND object_id = OBJECT_ID(N'[BadgeBatchItems]'))
BEGIN
    CREATE INDEX [IX_BadgeBatchItems_BadgeBatchId_DisplayOrder] ON [BadgeBatchItems] ([BadgeBatchId], [DisplayOrder]);
END
");

            // createindex IX_BadgeBatchItems_ProfileTypeId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BadgeBatchItems_ProfileTypeId' AND object_id = OBJECT_ID(N'[BadgeBatchItems]'))
BEGIN
    CREATE INDEX [IX_BadgeBatchItems_ProfileTypeId] ON [BadgeBatchItems] ([ProfileTypeId]);
END
");

            // createindex IX_ProfileIdentityDocuments_ProfileId_Kind
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProfileIdentityDocuments_ProfileId_Kind' AND object_id = OBJECT_ID(N'[ProfileIdentityDocuments]'))
BEGIN
    CREATE UNIQUE INDEX [IX_ProfileIdentityDocuments_ProfileId_Kind] ON [ProfileIdentityDocuments] ([ProfileId], [Kind]);
END
");

            // addconstraint FK_ArchiveEditions_StoredFiles_CoverImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_ArchiveEditions_StoredFiles_CoverImageFileId' AND parent_object_id = OBJECT_ID(N'[ArchiveEditions]'))
BEGIN
    ALTER TABLE [ArchiveEditions] ADD CONSTRAINT [FK_ArchiveEditions_StoredFiles_CoverImageFileId] FOREIGN KEY ([CoverImageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_ArchiveMediaItems_StoredFiles_MediaFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_ArchiveMediaItems_StoredFiles_MediaFileId' AND parent_object_id = OBJECT_ID(N'[ArchiveMediaItems]'))
BEGIN
    ALTER TABLE [ArchiveMediaItems] ADD CONSTRAINT [FK_ArchiveMediaItems_StoredFiles_MediaFileId] FOREIGN KEY ([MediaFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_ArchivePastSpeakers_StoredFiles_PhotoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_ArchivePastSpeakers_StoredFiles_PhotoFileId' AND parent_object_id = OBJECT_ID(N'[ArchivePastSpeakers]'))
BEGIN
    ALTER TABLE [ArchivePastSpeakers] ADD CONSTRAINT [FK_ArchivePastSpeakers_StoredFiles_PhotoFileId] FOREIGN KEY ([PhotoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Banners_StoredFiles_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Banners_StoredFiles_ImageFileId' AND parent_object_id = OBJECT_ID(N'[Banners]'))
BEGIN
    ALTER TABLE [Banners] ADD CONSTRAINT [FK_Banners_StoredFiles_ImageFileId] FOREIGN KEY ([ImageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Booths_StoredFiles_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Booths_StoredFiles_LogoFileId' AND parent_object_id = OBJECT_ID(N'[Booths]'))
BEGIN
    ALTER TABLE [Booths] ADD CONSTRAINT [FK_Booths_StoredFiles_LogoFileId] FOREIGN KEY ([LogoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Exhibitors_StoredFiles_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Exhibitors_StoredFiles_LogoFileId' AND parent_object_id = OBJECT_ID(N'[Exhibitors]'))
BEGIN
    ALTER TABLE [Exhibitors] ADD CONSTRAINT [FK_Exhibitors_StoredFiles_LogoFileId] FOREIGN KEY ([LogoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_GateScans_UserProfiles_UserProfileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_GateScans_UserProfiles_UserProfileId' AND parent_object_id = OBJECT_ID(N'[GateScans]'))
BEGIN
    ALTER TABLE [GateScans] ADD CONSTRAINT [FK_GateScans_UserProfiles_UserProfileId] FOREIGN KEY ([UserProfileId]) REFERENCES [UserProfiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_MediaItems_StoredFiles_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_MediaItems_StoredFiles_ImageFileId' AND parent_object_id = OBJECT_ID(N'[MediaItems]'))
BEGIN
    ALTER TABLE [MediaItems] ADD CONSTRAINT [FK_MediaItems_StoredFiles_ImageFileId] FOREIGN KEY ([ImageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_MediaItems_StoredFiles_VideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_MediaItems_StoredFiles_VideoFileId' AND parent_object_id = OBJECT_ID(N'[MediaItems]'))
BEGIN
    ALTER TABLE [MediaItems] ADD CONSTRAINT [FK_MediaItems_StoredFiles_VideoFileId] FOREIGN KEY ([VideoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_MediaPartners_StoredFiles_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_MediaPartners_StoredFiles_LogoFileId' AND parent_object_id = OBJECT_ID(N'[MediaPartners]'))
BEGIN
    ALTER TABLE [MediaPartners] ADD CONSTRAINT [FK_MediaPartners_StoredFiles_LogoFileId] FOREIGN KEY ([LogoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_News_StoredFiles_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_News_StoredFiles_ImageFileId' AND parent_object_id = OBJECT_ID(N'[News]'))
BEGIN
    ALTER TABLE [News] ADD CONSTRAINT [FK_News_StoredFiles_ImageFileId] FOREIGN KEY ([ImageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_OrganizationProfile_StoredFiles_BackgroundVideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_OrganizationProfile_StoredFiles_BackgroundVideoFileId' AND parent_object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    ALTER TABLE [OrganizationProfile] ADD CONSTRAINT [FK_OrganizationProfile_StoredFiles_BackgroundVideoFileId] FOREIGN KEY ([BackgroundVideoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_OrganizationProfile_StoredFiles_LiveStreamFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_OrganizationProfile_StoredFiles_LiveStreamFileId' AND parent_object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    ALTER TABLE [OrganizationProfile] ADD CONSTRAINT [FK_OrganizationProfile_StoredFiles_LiveStreamFileId] FOREIGN KEY ([LiveStreamFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_OrganizationProfile_StoredFiles_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_OrganizationProfile_StoredFiles_LogoFileId' AND parent_object_id = OBJECT_ID(N'[OrganizationProfile]'))
BEGIN
    ALTER TABLE [OrganizationProfile] ADD CONSTRAINT [FK_OrganizationProfile_StoredFiles_LogoFileId] FOREIGN KEY ([LogoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_ProgrammeDays_StoredFiles_ImageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_ProgrammeDays_StoredFiles_ImageFileId' AND parent_object_id = OBJECT_ID(N'[ProgrammeDays]'))
BEGIN
    ALTER TABLE [ProgrammeDays] ADD CONSTRAINT [FK_ProgrammeDays_StoredFiles_ImageFileId] FOREIGN KEY ([ImageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Sessions_StoredFiles_LiveSignLanguageFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Sessions_StoredFiles_LiveSignLanguageFileId' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [FK_Sessions_StoredFiles_LiveSignLanguageFileId] FOREIGN KEY ([LiveSignLanguageFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Sessions_StoredFiles_LiveStreamFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Sessions_StoredFiles_LiveStreamFileId' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [FK_Sessions_StoredFiles_LiveStreamFileId] FOREIGN KEY ([LiveStreamFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Sessions_StoredFiles_RecordingFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Sessions_StoredFiles_RecordingFileId' AND parent_object_id = OBJECT_ID(N'[Sessions]'))
BEGIN
    ALTER TABLE [Sessions] ADD CONSTRAINT [FK_Sessions_StoredFiles_RecordingFileId] FOREIGN KEY ([RecordingFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_SessionSummaries_StoredFiles_SummaryVideoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_SessionSummaries_StoredFiles_SummaryVideoFileId' AND parent_object_id = OBJECT_ID(N'[SessionSummaries]'))
BEGIN
    ALTER TABLE [SessionSummaries] ADD CONSTRAINT [FK_SessionSummaries_StoredFiles_SummaryVideoFileId] FOREIGN KEY ([SummaryVideoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_SpeakerPresentations_StoredFiles_StoredFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_SpeakerPresentations_StoredFiles_StoredFileId' AND parent_object_id = OBJECT_ID(N'[SpeakerPresentations]'))
BEGIN
    ALTER TABLE [SpeakerPresentations] ADD CONSTRAINT [FK_SpeakerPresentations_StoredFiles_StoredFileId] FOREIGN KEY ([StoredFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Speakers_StoredFiles_PhotoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Speakers_StoredFiles_PhotoFileId' AND parent_object_id = OBJECT_ID(N'[Speakers]'))
BEGIN
    ALTER TABLE [Speakers] ADD CONSTRAINT [FK_Speakers_StoredFiles_PhotoFileId] FOREIGN KEY ([PhotoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // addconstraint FK_Sponsors_StoredFiles_LogoFileId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE name = N'FK_Sponsors_StoredFiles_LogoFileId' AND parent_object_id = OBJECT_ID(N'[Sponsors]'))
BEGIN
    ALTER TABLE [Sponsors] ADD CONSTRAINT [FK_Sponsors_StoredFiles_LogoFileId] FOREIGN KEY ([LogoFileId]) REFERENCES [StoredFiles] ([Id]) ON DELETE NO ACTION;
END
");

            // passthrough
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.UserProfiles', 'NationalId') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''NationalId'', p.NationalId, ISNULL(p.NationalIdHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.NationalId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''NationalId'');');
");

            // passthrough
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.UserProfiles', 'IqamaNumber') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''Iqama'', p.IqamaNumber, ISNULL(p.IqamaNumberHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.IqamaNumber IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''Iqama'');');
");

            // passthrough
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.UserProfiles', 'PassportNumber') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''Passport'', p.PassportNumber, ISNULL(p.PassportNumberHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.PassportNumber IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''Passport'');');
");

            // drop UserProfiles.NationalId
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'NationalId') IS NOT NULL
BEGIN
    DECLARE @var124 nvarchar(max);
    SELECT @var124 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'NationalId');
    IF @var124 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var124 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [NationalId];
END
");

            // drop UserProfiles.NationalIdHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'NationalIdHash') IS NOT NULL
BEGIN
    DECLARE @var125 nvarchar(max);
    SELECT @var125 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'NationalIdHash');
    IF @var125 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var125 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [NationalIdHash];
END
");

            // drop UserProfiles.IqamaNumber
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'IqamaNumber') IS NOT NULL
BEGIN
    DECLARE @var126 nvarchar(max);
    SELECT @var126 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'IqamaNumber');
    IF @var126 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var126 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [IqamaNumber];
END
");

            // drop UserProfiles.IqamaNumberHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'IqamaNumberHash') IS NOT NULL
BEGIN
    DECLARE @var127 nvarchar(max);
    SELECT @var127 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'IqamaNumberHash');
    IF @var127 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var127 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [IqamaNumberHash];
END
");

            // drop UserProfiles.PassportNumberHash
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'PassportNumberHash') IS NOT NULL
BEGIN
    DECLARE @var128 nvarchar(max);
    SELECT @var128 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'PassportNumberHash');
    IF @var128 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var128 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [PassportNumberHash];
END
");

            // drop UserProfiles.PassportNumber
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[UserProfiles]', N'PassportNumber') IS NOT NULL
BEGIN
    DECLARE @var129 nvarchar(max);
    SELECT @var129 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserProfiles]') AND [c].[name] = N'PassportNumber');
    IF @var129 IS NOT NULL EXEC(N'ALTER TABLE [UserProfiles] DROP CONSTRAINT ' + @var129 + ';');
    ALTER TABLE [UserProfiles] DROP COLUMN [PassportNumber];
END
");

            // passthrough
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.BadgeBatches', 'TotalCount') IS NOT NULL ALTER TABLE dbo.BadgeBatches ALTER COLUMN [TotalCount] int NULL;
IF COL_LENGTH('dbo.BadgeBatches', 'CountsSummary') IS NOT NULL ALTER TABLE dbo.BadgeBatches ALTER COLUMN [CountsSummary] nvarchar(512) NULL;
");
        }

        /// <inheritdoc />
        /// <remarks>
        /// NOT REVERSIBLE, and deliberately so.
        ///
        /// <para>Up() is not a pure schema change. It copies every stored identity
        /// number out of <c>UserProfiles</c> into <c>ProfileIdentityDocuments</c>
        /// before dropping the source columns, and it retains three further columns
        /// that EF wanted to drop because they hold the only copy of their data.
        /// EF's generated Down() reversed neither: it re-created the identity
        /// columns EMPTY, leaving the numbers stranded in the child table, and it
        /// re-added three columns that were never removed, which fails outright
        /// with a duplicate-column error.</para>
        ///
        /// <para>A Down() that silently returns the database to a structurally
        /// plausible but data-wrong state is worse than one that refuses. This
        /// migration re-baselines live production databases; rolling it back is
        /// not an operation anyone should reach for by accident. Restore from
        /// backup instead - the documented procedure captures one first.</para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "20260907080012_SyncBaseline cannot be reverted. It moves identity "
                + "numbers into ProfileIdentityDocuments and retains columns EF "
                + "would have dropped, so no generated Down() can restore both the "
                + "schema and the data. Restore the database from the backup taken "
                + "before it was applied.");
    }
}
