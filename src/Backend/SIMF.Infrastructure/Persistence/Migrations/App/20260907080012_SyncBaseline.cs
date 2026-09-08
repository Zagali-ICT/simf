using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIMF.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class SyncBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DelegationMeetingRequests_DelegationAvailabilityWindows_AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_GateScans_UserProfiles_UserProfileId",
                table: "GateScans");

            migrationBuilder.DropForeignKey(
                name: "FK_HallAttendances_Halls_HallId",
                table: "HallAttendances");

            migrationBuilder.DropIndex(
                name: "IX_VisitorShareTokens_Token",
                table: "VisitorShareTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_IqamaNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_NationalIdHash",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_PassportNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_Speakers_UserProfileId",
                table: "Speakers");

            migrationBuilder.DropIndex(
                name: "IX_SessionSummaries_IsActive_PublishedAt",
                table: "SessionSummaries");

            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_SessionId_IsHidden_Order",
                table: "SessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_SubmittedByUserId",
                table: "SessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_SessionOutcomes_SessionId_IsActive_DisplayOrder",
                table: "SessionOutcomes");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_Expires",
                table: "SeatReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Halls_Geofence",
                table: "Halls");

            migrationBuilder.DropIndex(
                name: "IX_HallAttendances_HallId_Leave",
                table: "HallAttendances");

            migrationBuilder.DropIndex(
                name: "IX_DelegationMeetingRequests_AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            // RETAINED, NOT DROPPED - the owner's standing rule is that nothing is deleted.
            // Three columns EF wanted to drop still hold the only copy of their data, and
            // their replacements cannot be filled in SQL: MediaPartners.LogoRelativePath
            // needs a StoredFiles row with real bytes behind it, and BadgeBatches.TotalCount
            // and CountsSummary need the per-profile-type breakdown only BadgeBatchItems can
            // carry. Their DropColumn calls are removed rather than reordered. The columns
            // stay in the database unmapped by the model, which EF neither reads nor writes;
            // the two NOT NULL ones are made nullable at the end of this method so an INSERT
            // that omits them still succeeds. Backfill in the application, then drop them in
            // a migration of their own.
            migrationBuilder.DropColumn(
                name: "LogoRelativePath",
                table: "Sponsors");

            migrationBuilder.DropColumn(
                name: "PhotoRelativePath",
                table: "Speakers");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "SpeakerPresentations");

            migrationBuilder.DropColumn(
                name: "SummaryVideoUrl",
                table: "SessionSummaries");

            migrationBuilder.DropColumn(
                name: "LiveSignLanguageUrl",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LiveStreamUrl",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RatingPromptSent",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingContentType",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingFileName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingSizeBytes",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RecordingStoredFileName",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "SessionQuestions");

            // CORRECTED: Expires is RENAMED, not dropped - see the transposition
            // note above. IX_SeatReservations_Expires is dropped earlier in this method
            // and recreated as IX_SeatReservations_NoShowReleaseAt further down.
            migrationBuilder.RenameColumn(
                name: "Expires",
                table: "SeatReservations",
                newName: "NoShowReleaseAt");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "SeatReservations");

            migrationBuilder.DropColumn(
                name: "ResponseHash",
                table: "ScanIdempotency");

            migrationBuilder.DropColumn(
                name: "BackgroundVideoUrl",
                table: "OrganizationProfile");

            migrationBuilder.DropColumn(
                name: "CurrentYear",
                table: "OrganizationProfile");

            migrationBuilder.DropColumn(
                name: "LiveStreamUrl",
                table: "OrganizationProfile");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "OrganizationProfile");

            migrationBuilder.DropColumn(
                name: "VersionDate",
                table: "OrganizationProfile");

            migrationBuilder.DropColumn(
                name: "ImageRelativePath",
                table: "News");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "HallId",
                table: "HallAttendances");

            migrationBuilder.DropColumn(
                name: "OpenedByUserId",
                table: "EventEdition");

            migrationBuilder.DropColumn(
                name: "AvailabilityWindowId",
                table: "DelegationMeetingRequests");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Banners");

            migrationBuilder.DropColumn(
                name: "PhotoRelativePath",
                table: "ArchivePastSpeakers");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "ArchiveMediaItems");

            migrationBuilder.DropColumn(
                name: "CoverImageRelativePath",
                table: "ArchiveEditions");

            // CORRECTED. EF inferred a rename because both columns are
            // nvarchar(256) nullable on the same table in the same diff. They are
            // unrelated: PR 354 ADDED MobileNumber (superseding the SaudiMobile /
            // InternationalMobile pair) while PR 355 separately REMOVED PassportNumber
            // (moved to ProfileIdentityDocuments). Renaming would relabel every stored
            // passport number as the attendee's mobile number, decrypt cleanly, and
            // then be overwritten by the first ordinary profile save.
            migrationBuilder.AddColumn<string>(
                name: "MobileNumber",
                table: "UserProfiles",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "SecureDestroyed",
                table: "StoredFiles",
                newName: "SecureDestroyedAt");

            // CORRECTED. Both uniqueidentifier, but the decisions log dropped the uploader
            // copy in favour of StoredFile.CreatedBy and added StoredFileId as the FK.
            // A rename would file a user id as a file id.
            migrationBuilder.AddColumn<Guid>(
                name: "StoredFileId",
                table: "SpeakerPresentations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.DropColumn(
                name: "UploadedByUserId",
                table: "SpeakerPresentations");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "SpeakerMeetingRequests",
                newName: "ReminderSentAt");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "Sessions",
                newName: "ReminderSentAt");

            // CORRECTED. The two columns COEXISTED on Session.cs, so neither
            // can be a rename of the other: a uploader user id is not a file id.
            migrationBuilder.AddColumn<Guid>(
                name: "RecordingFileId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RecordingUploadedByUserId",
                table: "Sessions");

            // CORRECTED. A recording-upload timestamp is not a rating-prompt
            // timestamp; EF paired them only because both are datetime2 nullable.
            migrationBuilder.AddColumn<DateTime>(
                name: "RatingPromptSentAt",
                table: "Sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RecordingUploadedAt",
                table: "Sessions");

            migrationBuilder.RenameColumn(
                name: "ReviewedByUserId",
                table: "SeatReservations",
                newName: "ReleasedByUserId");

            // CORRECTED - EF TRANSPOSED THIS PAIR. the decisions log and commit 21f065be9
            // record that Expires became NoShowReleaseAt and ReviewedAt was dropped
            // outright (ReleasedAt already existed). EF had it the other way round, which
            // would discard every no-show deadline and fill NoShowReleaseAt from
            // ReviewedAt - null on every live row - silently stopping the no-show sweep,
            // whose filtered index is [ReleasedAt] IS NULL AND [NoShowReleaseAt] IS NOT NULL.
            // The neighbouring ReviewedByUserId -> ReleasedByUserId rename IS genuine.
            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "SeatReservations");

            migrationBuilder.RenameColumn(
                name: "AllowsVipMeetingSlots",
                table: "ProfileTypes",
                newName: "IsVipTier");

            // CORRECTED. ImageFileId, VideoFileId and ThumbnailFileId coexisted
            // on MediaItem.cs; a thumbnail pointer is not a video pointer.
            migrationBuilder.AddColumn<Guid>(
                name: "VideoFileId",
                table: "MediaItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ThumbnailFileId",
                table: "MediaItems");

            migrationBuilder.RenameColumn(
                name: "EquipmentNotes",
                table: "Halls",
                newName: "FacilityNotes");

            migrationBuilder.RenameColumn(
                name: "CreateBy",
                table: "GateAssignments",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "ReminderSent",
                table: "DelegationMeetingRequests",
                newName: "ReminderSentAt");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "VisitorShareTokens",
                type: "varchar(256)",
                unicode: false,
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "VisitorShareTokens",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "LabelArabic",
                table: "VenueMapNodes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "RejectionReasonArabic",
                table: "UserProfiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "UserProfiles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "JobTitleArabic",
                table: "UserProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HonorificArabic",
                table: "UserProfiles",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrganisationOther",
                table: "UserProfiles",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Themes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionArabic",
                table: "Themes",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Sha256",
                table: "StoredFiles",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "KekVersion",
                table: "StoredFiles",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TaglineArabic",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Sponsors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CityArabic",
                table: "Sponsors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AboutArabic",
                table: "Sponsors",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "Sponsors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TrainingExperienceArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RankArabic",
                table: "Speakers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QualificationsArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Speakers",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "CityArabic",
                table: "Speakers",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BioArabic",
                table: "Speakers",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AwardsArabic",
                table: "Speakers",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoFileId",
                table: "Speakers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SpeakersArabic",
                table: "SessionSummaries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "RecommendationsArabic",
                table: "SessionSummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "KeyPointsArabic",
                table: "SessionSummaries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "FullTextArabic",
                table: "SessionSummaries",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000);

            migrationBuilder.AlterColumn<string>(
                name: "AiDraftFullTextArabic",
                table: "SessionSummaries",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SummaryVideoFileId",
                table: "SessionSummaries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "Sessions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "LiveNoticeArabic",
                table: "Sessions",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LiveCaptionsArabic",
                table: "Sessions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LanguageArabic",
                table: "Sessions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionArabic",
                table: "Sessions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LiveSignLanguageFileId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LiveStreamFileId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TextArabic",
                table: "SessionOutcomes",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "SessionCategories",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "GuestHintArabic",
                table: "SeatReservations",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RequestHash",
                table: "ScanIdempotency",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Regions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "RatingTypes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "CommentLabelArabic",
                table: "RatingTypes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TextArabic",
                table: "RatingQuestions",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "RatingQuestionGroups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "ProgrammeDays",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageFileId",
                table: "ProgrammeDays",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "ProfileTypes",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "OrganizationProfile",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "SloganArabic",
                table: "OrganizationProfile",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RegistrationSuccessMessageArabic",
                table: "OrganizationProfile",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "OrganizationProfile",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "LocationTextArabic",
                table: "OrganizationProfile",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BioArabic",
                table: "OrganizationProfile",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BackgroundVideoFileId",
                table: "OrganizationProfile",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LiveStreamFileId",
                table: "OrganizationProfile",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "OrganizationProfile",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ValueArabic",
                table: "OrganizationDetails",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "OrganizationDetails",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "OrganizationAboutItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "TextArabic",
                table: "OrganizationAboutItems",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Organisations",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "CommercialRegistration",
                table: "Organisations",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "NotificationBroadcasts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "BodyArabic",
                table: "NotificationBroadcasts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "News",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "ExcerptArabic",
                table: "News",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CategoryArabic",
                table: "News",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "BodyArabic",
                table: "News",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageFileId",
                table: "News",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "MeetingActionTokens",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "MediaPartners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CityArabic",
                table: "MediaPartners",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "MediaPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "MediaItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AlbumArabic",
                table: "MediaItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Interests",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Halls",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Gates",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionArabic",
                table: "Gates",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "FaqGroups",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "QuestionArabic",
                table: "FaqEntries",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "AnswerArabic",
                table: "FaqEntries",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Exhibitors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CityArabic",
                table: "Exhibitors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "Exhibitors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "DelegationMeetingActionTokens",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Countries",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ContentArabic",
                table: "ContentBlocks",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 8000);

            migrationBuilder.AddColumn<Guid>(
                name: "PairHighUserId",
                table: "Connections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PairLowUserId",
                table: "Connections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SectorArabic",
                table: "Booths",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OfficerNameArabic",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OfficerCityArabic",
                table: "Booths",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "Booths",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "ExhibitorNameArabic",
                table: "Booths",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionArabic",
                table: "Booths",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoFileId",
                table: "Booths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleArabic",
                table: "Banners",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "BodyArabic",
                table: "Banners",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageFileId",
                table: "Banners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameArabic",
                table: "BadgeBatches",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoFileId",
                table: "ArchivePastSpeakers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaFileId",
                table: "ArchiveMediaItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverImageFileId",
                table: "ArchiveEditions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayNameArabic",
                table: "AiPrompts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "DescriptionArabic",
                table: "AiPrompts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                collation: "Arabic_CI_AI",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "AiChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.CreateTable(
                name: "BadgeBatchItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BadgeBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BadgeBatchItems_BadgeBatches_BadgeBatchId",
                        column: x => x.BadgeBatchId,
                        principalTable: "BadgeBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BadgeBatchItems_ProfileTypes_ProfileTypeId",
                        column: x => x.ProfileTypeId,
                        principalTable: "ProfileTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProfileIdentityDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Number = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NumberHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileIdentityDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileIdentityDocuments_UserProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Organisations",
                columns: new[] { "Id", "City", "CommercialRegistration", "CreatedAt", "CreatedBy", "DeletedAt", "Email", "IsActive", "Name", "NameArabic", "Phone", "Sector", "UpdatedAt", "UpdatedBy", "Website" },
                values: new object[] { new Guid("a17e9c42-0b6d-4f58-9e31-7c2a8d5f60b4"), null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new Guid("00000000-0000-0000-0000-000000000000"), null, null, true, "Other", "أخرى", null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "OrganizationProfile",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "BackgroundVideoFileId", "EventEndDate", "EventStartDate", "LiveStreamFileId", "LogoFileId" },
                values: new object[] { null, new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 11, 23, 0, 0, 0, 0, DateTimeKind.Unspecified), null, null });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorShareTokens_TokenHash",
                table: "VisitorShareTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_VisitorShareTokens_RevocationPin",
                table: "VisitorShareTokens",
                sql: "([IsActive] = 1 AND [RevokedAt] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserProfiles_AccessibilityTextSize",
                table: "UserProfiles",
                sql: "[AccessibilityTextSize] IN ('small', 'normal', 'large', 'extraLarge')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Themes_DisplayOrder",
                table: "Themes",
                sql: "[DisplayOrder] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_KekVersion",
                table: "StoredFiles",
                column: "KekVersion",
                filter: "[IsEncrypted] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_Service_OwnerEntityId",
                table: "StoredFiles",
                columns: new[] { "Service", "OwnerEntityId" },
                unique: true,
                filter: "[IsActive] = 1 AND [OwnerEntityId] IS NOT NULL AND [Service] IN (0, 4, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 23, 24)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StoredFiles_SizeBytes",
                table: "StoredFiles",
                sql: "[SizeBytes] IS NULL OR [SizeBytes] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_LogoFileId",
                table: "Sponsors",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sponsors_Tier_NameArabic",
                table: "Sponsors",
                columns: new[] { "Tier", "NameArabic" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sponsors_Coordinates",
                table: "Sponsors",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_PhotoFileId",
                table: "Speakers",
                column: "PhotoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Speakers_UserProfileId",
                table: "Speakers",
                column: "UserProfileId",
                unique: true,
                filter: "[UserProfileId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Speakers_DisplayOrder",
                table: "Speakers",
                sql: "[DisplayOrder] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Speakers_Location",
                table: "Speakers",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerPresentations_StoredFileId",
                table: "SpeakerPresentations",
                column: "StoredFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SpeakerMeetingRequests_RequestedByUserId_SpeakerId",
                table: "SpeakerMeetingRequests",
                columns: new[] { "RequestedByUserId", "SpeakerId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SpeakerAvailabilityWindows_SlotMinutes",
                table: "SpeakerAvailabilityWindows",
                sql: "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");

            migrationBuilder.CreateIndex(
                name: "IX_SessionSummaries_SummaryVideoFileId",
                table: "SessionSummaries",
                column: "SummaryVideoFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionSummaries_PublishPin",
                table: "SessionSummaries",
                sql: "([PublishedAt] IS NULL AND [PublishedByUserId] IS NULL) OR ([PublishedAt] IS NOT NULL AND [PublishedByUserId] IS NOT NULL AND [ApprovedAt] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LiveSignLanguageFileId",
                table: "Sessions",
                column: "LiveSignLanguageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_LiveStreamFileId",
                table: "Sessions",
                column: "LiveStreamFileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_RecordingFileId",
                table: "Sessions",
                column: "RecordingFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_CapacityOverride",
                table: "Sessions",
                sql: "[CapacityOverride] IS NULL OR [CapacityOverride] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_PublishedAtPin",
                table: "Sessions",
                sql: "([Status] = 3 AND [PublishedAt] IS NOT NULL) OR ([Status] <> 3 AND [PublishedAt] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_RecordedHasRecording",
                table: "Sessions",
                sql: "[Status] NOT IN (2, 3) OR [RecordingFileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_SessionId_IsPushed_Order",
                table: "SessionQuestions",
                columns: new[] { "SessionId", "IsPushed", "Order" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionQuestions_EscalationTrio",
                table: "SessionQuestions",
                sql: "([AssignedToRole] IS NULL AND [EscalatedByUserId] IS NULL AND [EscalatedAt] IS NULL) OR ([AssignedToRole] IS NOT NULL AND [EscalatedByUserId] IS NOT NULL AND [EscalatedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionQuestions_PushedPair",
                table: "SessionQuestions",
                sql: "([IsPushed] = 0 AND [PushedAt] IS NULL) OR ([IsPushed] = 1 AND [PushedAt] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_SessionOutcomes_SessionId_DisplayOrder",
                table: "SessionOutcomes",
                columns: new[] { "SessionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_NoShowReleaseAt",
                table: "SeatReservations",
                column: "NoShowReleaseAt",
                filter: "[ReleasedAt] IS NULL AND [NoShowReleaseAt] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_AdminBlockHasNoHolder",
                table: "SeatReservations",
                sql: "([Kind] = 1 AND [ReservedForProfileId] IS NULL) OR ([Kind] <> 1 AND [ReservedForProfileId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_ReleasePin",
                table: "SeatReservations",
                sql: "[ReleasedAt] IS NULL OR [Status] = 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_SeatNumber",
                table: "SeatReservations",
                sql: "[SeatNumber] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SeatReservations_SeatPair",
                table: "SeatReservations",
                sql: "([RowLabel] IS NULL AND [SeatNumber] IS NULL) OR ([RowLabel] IS NOT NULL AND [SeatNumber] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SavedContacts_NotSelf",
                table: "SavedContacts",
                sql: "[OwnerUserId] <> [SubjectUserId]");

            migrationBuilder.CreateIndex(
                name: "IX_RatingResponses_TargetId_IsActive",
                table: "RatingResponses",
                columns: new[] { "TargetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeDays_ImageFileId",
                table: "ProgrammeDays",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProfile_BackgroundVideoFileId",
                table: "OrganizationProfile",
                column: "BackgroundVideoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProfile_LiveStreamFileId",
                table: "OrganizationProfile",
                column: "LiveStreamFileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationProfile_LogoFileId",
                table: "OrganizationProfile",
                column: "LogoFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationProfile_Coordinates",
                table: "OrganizationProfile",
                sql: "([Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)) AND ([Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationProfile_EventWindow",
                table: "OrganizationProfile",
                sql: "[EventStartDate] IS NULL OR [EventEndDate] IS NULL OR [EventEndDate] >= [EventStartDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationBroadcasts_TargetArc",
                table: "NotificationBroadcasts",
                sql: "([TargetMode] = 'Session' AND [SessionId] IS NOT NULL AND [AudienceScope] IS NULL) OR ([TargetMode] = 'Audience' AND [AudienceScope] IS NOT NULL AND [SessionId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_News_ImageFileId",
                table: "News",
                column: "ImageFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MeetingTables_Capacity",
                table: "MeetingTables",
                sql: "[Capacity] >= 2 AND [Capacity] <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_LogoFileId",
                table: "MediaPartners",
                column: "LogoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPartners_Name",
                table: "MediaPartners",
                column: "Name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MediaPartners_Coordinates",
                table: "MediaPartners",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_ImageFileId",
                table: "MediaItems",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_VideoFileId",
                table: "MediaItems",
                column: "VideoFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_ResponsePin",
                table: "Invitations",
                sql: "([State] = 0 AND [RespondedAt] IS NULL) OR ([State] <> 0 AND [RespondedAt] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallSeatLayouts_SeatsPerRow",
                table: "HallSeatLayouts",
                sql: "[SeatsPerRow] >= 1 AND [SeatsPerRow] <= 80");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Halls_Capacity",
                table: "Halls",
                sql: "[Capacity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Halls_Geofence",
                table: "Halls",
                sql: "([GeofenceCenterLat] IS NULL AND [GeofenceCenterLon] IS NULL AND [GeofenceRadiusMeters] IS NULL) OR ([GeofenceCenterLat] IS NOT NULL AND [GeofenceCenterLon] IS NOT NULL AND [GeofenceRadiusMeters] IS NOT NULL AND [GeofenceCenterLat] >= -90 AND [GeofenceCenterLat] <= 90 AND [GeofenceCenterLon] >= -180 AND [GeofenceCenterLon] <= 180 AND [GeofenceRadiusMeters] > 0 AND [GeofenceRadiusMeters] <= 100000)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAvailabilityWindows_SlotMinutes",
                table: "HallAvailabilityWindows",
                sql: "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");

            migrationBuilder.CreateIndex(
                name: "IX_HallAttendances_SessionId_UserProfileId_Leave",
                table: "HallAttendances",
                columns: new[] { "SessionId", "UserProfileId", "Leave" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAttendances_LeaveOrder",
                table: "HallAttendances",
                sql: "[Leave] IS NULL OR [Leave] >= [Enter]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAllocations_RowColumnSpec",
                table: "HallAllocations",
                sql: "([Mode] = 2 AND [RowColumnSpec] IS NOT NULL) OR ([Mode] <> 2 AND [RowColumnSpec] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HallAllocations_UnitCount",
                table: "HallAllocations",
                sql: "([Mode] = 1 AND [UnitCount] >= 1) OR ([Mode] <> 1 AND [UnitCount] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GateScan_UserProfile_ScannedAt",
                table: "GateScans",
                columns: new[] { "UserProfileId", "ScannedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_GateScans_DenialReasonRange",
                table: "GateScans",
                sql: "[DenialReasonCode] IS NULL OR [DenialReasonCode] BETWEEN 0 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GateScans_DirectionRange",
                table: "GateScans",
                sql: "[Direction] BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GateScans_SourceRange",
                table: "GateScans",
                sql: "[Source] BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gates_DirectionModeRange",
                table: "Gates",
                sql: "[DirectionMode] BETWEEN 0 AND 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GateAssignments_RevocationPin",
                table: "GateAssignments",
                sql: "([IsActive] = 1 AND [RevokedAt] IS NULL AND [RevokedByUserId] IS NULL) OR ([IsActive] = 0 AND [RevokedAt] IS NOT NULL AND [RevokedByUserId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Exhibitors_LogoFileId",
                table: "Exhibitors",
                column: "LogoFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exhibitors_Coordinates",
                table: "Exhibitors",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EventEdition_Year",
                table: "EventEdition",
                sql: "[Year] BETWEEN 2000 AND 2999");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DevicePositionPings_Coordinates",
                table: "DevicePositionPings",
                sql: "[Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180");

            migrationBuilder.CreateIndex(
                name: "IX_DelegationMeetingRequests_RequestedByUserId_TargetCountryId",
                table: "DelegationMeetingRequests",
                columns: new[] { "RequestedByUserId", "TargetCountryId" },
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationMeetingRequests_AttendeeCount",
                table: "DelegationMeetingRequests",
                sql: "[AttendeeCount] >= 1 AND [AttendeeCount] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationMeetingRequests_NotSelf",
                table: "DelegationMeetingRequests",
                sql: "[RequestingCountryId] <> [TargetCountryId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DelegationAvailabilityWindows_SlotMinutes",
                table: "DelegationAvailabilityWindows",
                sql: "[SlotMinutes] >= 5 AND [SlotMinutes] <= 480");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Countries_DelegationWindow",
                table: "Countries",
                sql: "[DelegationArrivalDate] IS NULL OR [DelegationDepartureDate] IS NULL OR [DelegationDepartureDate] >= [DelegationArrivalDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContactInquiries_HandledPin",
                table: "ContactInquiries",
                sql: "([IsHandled] = 0 AND [HandledAt] IS NULL AND [HandledByUserId] IS NULL) OR ([IsHandled] = 1 AND [HandledAt] IS NOT NULL AND [HandledByUserId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Connections_PairLowUserId_PairHighUserId",
                table: "Connections",
                columns: new[] { "PairLowUserId", "PairHighUserId" },
                unique: true,
                filter: "[IsActive] = 1 AND [PairLowUserId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Connections_NotSelf",
                table: "Connections",
                sql: "[RequesterUserId] <> [TargetUserId]");

            migrationBuilder.CreateIndex(
                name: "IX_Booths_LogoFileId",
                table: "Booths",
                column: "LogoFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Booths_OfficerCoordinates",
                table: "Booths",
                sql: "([OfficerLatitude] IS NULL AND [OfficerLongitude] IS NULL) OR ([OfficerLatitude] IS NOT NULL AND [OfficerLongitude] IS NOT NULL AND [OfficerLatitude] >= -90 AND [OfficerLatitude] <= 90 AND [OfficerLongitude] >= -180 AND [OfficerLongitude] <= 180)");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_ImageFileId",
                table: "Banners",
                column: "ImageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivePastSpeakers_PhotoFileId",
                table: "ArchivePastSpeakers",
                column: "PhotoFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveMediaItems_MediaFileId",
                table: "ArchiveMediaItems",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveEditions_CoverImageFileId",
                table: "ArchiveEditions",
                column: "CoverImageFileId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchiveEditions_CountersNonNegative",
                table: "ArchiveEditions",
                sql: "[Attendees] >= 0 AND [Sessions] >= 0 AND [Speakers] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ArchiveEditions_YearRange",
                table: "ArchiveEditions",
                sql: "[Year] >= 2000 AND [Year] <= 2100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiPrompts_MaxOutputTokens",
                table: "AiPrompts",
                sql: "[MaxOutputTokens] >= 1 AND [MaxOutputTokens] <= 8000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiPrompts_Temperature",
                table: "AiPrompts",
                sql: "[Temperature] >= 0 AND [Temperature] <= 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiInvocations_CallerKind",
                table: "AiInvocations",
                sql: "[CallerKind] IN ('Anonymous', 'Visitor', 'Staff', 'Admin', 'Moderator')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiChatMessages_Role",
                table: "AiChatMessages",
                sql: "[Role] IN ('user', 'assistant')");

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatchItems_BadgeBatchId_DisplayOrder",
                table: "BadgeBatchItems",
                columns: new[] { "BadgeBatchId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BadgeBatchItems_ProfileTypeId",
                table: "BadgeBatchItems",
                column: "ProfileTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileIdentityDocuments_ProfileId_Kind",
                table: "ProfileIdentityDocuments",
                columns: new[] { "ProfileId", "Kind" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchiveEditions_StoredFiles_CoverImageFileId",
                table: "ArchiveEditions",
                column: "CoverImageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchiveMediaItems_StoredFiles_MediaFileId",
                table: "ArchiveMediaItems",
                column: "MediaFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ArchivePastSpeakers_StoredFiles_PhotoFileId",
                table: "ArchivePastSpeakers",
                column: "PhotoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Banners_StoredFiles_ImageFileId",
                table: "Banners",
                column: "ImageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Booths_StoredFiles_LogoFileId",
                table: "Booths",
                column: "LogoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exhibitors_StoredFiles_LogoFileId",
                table: "Exhibitors",
                column: "LogoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GateScans_UserProfiles_UserProfileId",
                table: "GateScans",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_StoredFiles_ImageFileId",
                table: "MediaItems",
                column: "ImageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_StoredFiles_VideoFileId",
                table: "MediaItems",
                column: "VideoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaPartners_StoredFiles_LogoFileId",
                table: "MediaPartners",
                column: "LogoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_News_StoredFiles_ImageFileId",
                table: "News",
                column: "ImageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationProfile_StoredFiles_BackgroundVideoFileId",
                table: "OrganizationProfile",
                column: "BackgroundVideoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationProfile_StoredFiles_LiveStreamFileId",
                table: "OrganizationProfile",
                column: "LiveStreamFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationProfile_StoredFiles_LogoFileId",
                table: "OrganizationProfile",
                column: "LogoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgrammeDays_StoredFiles_ImageFileId",
                table: "ProgrammeDays",
                column: "ImageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_StoredFiles_LiveSignLanguageFileId",
                table: "Sessions",
                column: "LiveSignLanguageFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_StoredFiles_LiveStreamFileId",
                table: "Sessions",
                column: "LiveStreamFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_StoredFiles_RecordingFileId",
                table: "Sessions",
                column: "RecordingFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionSummaries_StoredFiles_SummaryVideoFileId",
                table: "SessionSummaries",
                column: "SummaryVideoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpeakerPresentations_StoredFiles_StoredFileId",
                table: "SpeakerPresentations",
                column: "StoredFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Speakers_StoredFiles_PhotoFileId",
                table: "Speakers",
                column: "PhotoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Sponsors_StoredFiles_LogoFileId",
                table: "Sponsors",
                column: "LogoFileId",
                principalTable: "StoredFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        

            // DATA MIGRATION, then the drops it protects.
            //
            // EF scheduled these five drops near the top of Up(), which would have destroyed
            // every stored identity number BEFORE ProfileIdentityDocuments (created further
            // up this method) could receive them. They are deferred to here so the copy runs
            // first. the decisions log moved the fact, it did not retire it.
            //
            // The value copied across is the AES-GCM CIPHERTEXT, which is correct: the
            // encryptor seals with no associated data and one key, so a ciphertext is not
            // bound to its column and decrypts identically from the new table.
            //
            // NumberHash is a keyed HMAC computed in application code and CANNOT be derived
            // in SQL. Where the source digest exists it is carried across; where it does not
            // - it was added later than the values it covers - an empty string is written to
            // satisfy the NOT NULL column. Nothing reads NumberHash today (the decisions log dropped its
            // unique index), so a blank digest is inert, but it MUST be backfilled by an
            // application pass before any document-number lookup is built on it.
            //
            // Guarded by COL_LENGTH and NOT EXISTS so it is safe against a database where
            // the columns are already gone or the rows already copied - which is the state
            // production may be in, its schema having drifted by hand.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.UserProfiles', 'NationalId') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''NationalId'', p.NationalId, ISNULL(p.NationalIdHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.NationalId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''NationalId'');');

IF COL_LENGTH('dbo.UserProfiles', 'IqamaNumber') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''Iqama'', p.IqamaNumber, ISNULL(p.IqamaNumberHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.IqamaNumber IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''Iqama'');');

IF COL_LENGTH('dbo.UserProfiles', 'PassportNumber') IS NOT NULL
EXEC(N'
INSERT INTO dbo.ProfileIdentityDocuments (Id, ProfileId, [Kind], [Number], NumberHash, CreatedAt, CreatedBy, IsActive)
SELECT NEWID(), p.Id, N''Passport'', p.PassportNumber, ISNULL(p.PassportNumberHash, N''''), SYSUTCDATETIME(), ''00000000-0000-0000-0000-000000000000'', 1
FROM dbo.UserProfiles p
WHERE p.PassportNumber IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.ProfileIdentityDocuments d WHERE d.ProfileId = p.Id AND d.[Kind] = N''Passport'');');
");

            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "NationalIdHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IqamaNumber",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "IqamaNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PassportNumberHash",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PassportNumber",
                table: "UserProfiles");

            // Keep the three retained columns writable by EF, which never names them in an
            // INSERT. A NOT NULL column with no default would fail every insert.
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
