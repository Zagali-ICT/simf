using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Files;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Session entity configuration.
/// Real DB FK to Hall (same DbContext). Composite-PK join tables
/// SessionSpeaker + SessionTheme persist the two M-to-M relations.</summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        // A session must end after it starts.
        // The arrival-grace override is null (inherit the hall) or within
        // the one shared bound; same rule as CK_Halls_ArrivalGrace.
        // The capacity override is null (inherit the hall) or zero-or-positive,
        // mirroring AdminSessionService.ValidateCapacity and CK_Halls_Capacity —
        // zero is a real value there too, so this is >= 0 and not > 0.
        // PublishedAt is pinned to the Published status: SetStatusAsync stamps the
        // two together and is the only writer of either column, so "published with
        // no stamp" (and "stamped but not published") are unreachable through the
        // service and are refused by the schema too — the shape
        // CK_ContactInquiries_HandledPin uses for its own paired columns.
        builder.ToTable("Sessions", table =>
        {
            table.HasCheckConstraint("CK_Sessions_TimeWindow", "[End] > [Start]");
            table.HasCheckConstraint(
                "CK_Sessions_ArrivalGrace",
                $"[ArrivalGraceMinutesOverride] IS NULL OR ([ArrivalGraceMinutesOverride] >= 0 AND [ArrivalGraceMinutesOverride] <= {WalkInModeOptions.MaxArrivalGraceMinutes})");
            table.HasCheckConstraint(
                "CK_Sessions_CapacityOverride",
                "[CapacityOverride] IS NULL OR [CapacityOverride] >= 0");
            table.HasCheckConstraint(
                "CK_Sessions_PublishedAtPin",
                $"([Status] = {(int)SessionStatus.Published} AND [PublishedAt] IS NOT NULL) OR "
                + $"([Status] <> {(int)SessionStatus.Published} AND [PublishedAt] IS NULL)");
        });
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(SessionRules.MaxCodeLength).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(SessionRules.MaxTitleLength).IsRequired();
        builder.Property(s => s.TitleArabic).HasMaxLength(SessionRules.MaxTitleLength).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2048);
        builder.Property(s => s.DescriptionArabic).HasMaxLength(2048);

        // Optional bilingual session-language label (public "at a glance" card).
        builder.Property(s => s.Language).HasMaxLength(64);
        builder.Property(s => s.LanguageArabic).HasMaxLength(64);

        // The recording's file, in the one store. Restrict, matching Hall and
        // Category below: deleting a file must never delete the session.
        //
        // The navigation replaces four columns that used to sit here holding the
        // recording's name, media type, size and uploader. The store owns all
        // four, so each upload wrote the same facts twice with nothing keeping
        // the pairs equal afterwards - and they were not equal, the store
        // canonicalising the media type where the copy kept the client's string.
        builder.HasIndex(s => s.RecordingFileId);
        builder.HasOne(s => s.RecordingFile)
            .WithMany()
            .HasForeignKey(s => s.RecordingFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // §8 — live broadcast stream URLs (manual stub provider).
        // The feeds are StoredFile rows now, so the key replaces the length cap:
        // the URL's own bound lives on StoredFile.ExternalUrl. Restrict, not
        // Cascade, for the reason the other file keys carry it - deleting a file
        // must never silently delete the session.
        builder.HasIndex(s => s.LiveStreamFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(s => s.LiveStreamFileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.LiveSignLanguageFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(s => s.LiveSignLanguageFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // AI live-caption text (manual stub provider, bilingual). 2048
        // matches the Description column — the SSOT the CP form + service-layer
        // validation align to.
        builder.Property(s => s.LiveCaptions).HasMaxLength(2048);
        builder.Property(s => s.LiveCaptionsArabic).HasMaxLength(2048);

        // The informational live notice
        // (bilingual). Shorter than the caption/description columns because it is
        // a one-line banner, not an abstract: 512 is the SSOT the CP form's
        // MaxLength + the service-layer length check align to (§7).
        builder.Property(s => s.LiveNotice).HasMaxLength(512);
        builder.Property(s => s.LiveNoticeArabic).HasMaxLength(512);

        // Deliberately UNFILTERED, unlike the ProgrammeDay and SessionCategory
        // uniqueness indexes, which exclude soft-deleted rows. Code is the
        // session's public URL segment, so a retired code stays reserved: a new
        // session must not inherit the address an old one's links still point at.
        // AdminSessionService checks the same way — Code == code with no IsActive
        // filter — so the index and the service agree.
        builder.HasIndex(s => s.Code).IsUnique();

        // Real DB FK to Hall — same context. Restrict matches the
        // soft-delete policy (admins deactivate halls; they never
        // hard-delete a hall a session points at).
        builder.HasOne(s => s.Hall)
            .WithMany()
            .HasForeignKey(s => s.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional real FK to the dynamic SessionCategory lookup.
        // Restrict (a category cannot be hard-deleted while a session points at
        // it; admins soft-delete via IsActive). HasForeignKey creates the index.
        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two query indexes the agenda screen + the operator views ride.
        builder.HasIndex(s => new { s.IsActive, s.Start });
        builder.HasIndex(s => new { s.HallId, s.Start });

        // The Committee's lifecycle queue lists sessions by
        // status (e.g. the Recorded ones awaiting publish), most-recent first.
        // Status is stored as int (enum, by convention); no HasDefaultValue —
        // the service always writes an explicit value (avoids the EF "0 looks
        // unset" default-backfill trap), and a new row takes Scheduled (0) from
        // the property initialiser on the entity.
        builder.HasIndex(s => new { s.Status, s.Start });
    }
}

/// <summary>SessionSummary (محضر) configuration. One summary per
/// session (unique <c>SessionId</c>), cascade-deleted with its session. Every
/// HasMaxLength here is the single source of truth the edit form + validator
/// align to (§7); the full-text columns match the News body length (8000).</summary>
internal sealed class SessionSummaryConfiguration
    : IEntityTypeConfiguration<SessionSummary>
{
    public void Configure(EntityTypeBuilder<SessionSummary> builder)
    {
        // Approval cannot precede review submission.
        builder.ToTable("SessionSummaries", table => table.HasCheckConstraint(
            "CK_SessionSummaries_ReviewOrder",
            "[ApprovedAt] IS NULL OR ([ReviewSubmittedAt] IS NOT NULL AND [ApprovedAt] >= [ReviewSubmittedAt])"));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.KeyPoints).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.KeyPointsArabic).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.Recommendations).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.RecommendationsArabic).HasMaxLength(4000).IsRequired();
        builder.Property(s => s.Speakers).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.SpeakersArabic).HasMaxLength(1000).IsRequired();
        builder.Property(s => s.FullText).HasMaxLength(8000).IsRequired();
        builder.Property(s => s.FullTextArabic).HasMaxLength(8000).IsRequired();
        builder.Property(s => s.AiModel).HasMaxLength(64);

        // The optional team summary-video feed. It was a URL column with its own
        // 1024 length cap; it is a StoredFile key now, exactly like the session's
        // own live feed, so the key replaces the cap and the URL's bound lives on
        // StoredFile.ExternalUrl. Restrict for the same reason the session's file
        // keys carry it: deleting a file must never delete the summary.
        builder.HasIndex(s => s.SummaryVideoFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(s => s.SummaryVideoFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // The pristine AI-draft snapshot mirrors the Arabic full-text it is
        // captured from (same 8000 SSOT). Nullable: only AI-generated summaries
        // carry it. Its companion AiDraftGeneratedAt is a nullable DateTime and
        // needs no configuration.
        builder.Property(s => s.AiDraftFullTextArabic).HasMaxLength(8000);

        // 1:1 — exactly one summary per session, cascade with the session.
        builder.HasIndex(s => s.SessionId).IsUnique();
        builder.HasOne(s => s.Session)
            .WithMany()
            .HasForeignKey(s => s.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The app reads published summaries; the Committee desk lists drafts.
        builder.HasIndex(s => new { s.IsActive, s.PublishedAt });
    }
}

internal sealed class SessionSpeakerConfiguration
    : IEntityTypeConfiguration<SessionSpeaker>
{
    public void Configure(EntityTypeBuilder<SessionSpeaker> builder)
    {
        builder.ToTable("SessionSpeakers");
        builder.HasKey(ss => new { ss.SessionId, ss.SpeakerId });

        // Speaker/host role on the join (stored as int).
        builder.Property(ss => ss.Role);

        builder.HasOne(ss => ss.Session)
            .WithMany(s => s.Speakers)
            .HasForeignKey(ss => ss.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ss => ss.Speaker)
            .WithMany()
            .HasForeignKey(ss => ss.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SessionThemeConfiguration
    : IEntityTypeConfiguration<SessionTheme>
{
    public void Configure(EntityTypeBuilder<SessionTheme> builder)
    {
        builder.ToTable("SessionThemes");
        builder.HasKey(st => new { st.SessionId, st.ThemeId });

        builder.HasOne(st => st.Session)
            .WithMany(s => s.Themes)
            .HasForeignKey(st => st.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(st => st.Theme)
            .WithMany()
            .HasForeignKey(st => st.ThemeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
