using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-165 (gap doc G3) — Session entity configuration.
/// Real DB FK to Hall (same DbContext). Composite-PK join tables
/// SessionSpeaker + SessionTheme persist the two M-to-M relations.</summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        // D-611 (Wave B) — a session must end after it starts.
        builder.ToTable("Sessions", table => table.HasCheckConstraint(
            "CK_Sessions_TimeWindow", "[EndUtc] > [StartUtc]"));
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).HasMaxLength(16).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(256).IsRequired();
        builder.Property(s => s.TitleArabic).HasMaxLength(256).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(2048);
        builder.Property(s => s.DescriptionArabic).HasMaxLength(2048);

        // Optional bilingual session-language label (public "at a glance" card).
        builder.Property(s => s.Language).HasMaxLength(64);
        builder.Property(s => s.LanguageArabic).HasMaxLength(64);

        // P3.2b — D-232: recording metadata (the bytes live out-of-row on disk).
        builder.Property(s => s.RecordingStoredFileName).HasMaxLength(64);
        builder.Property(s => s.RecordingFileName).HasMaxLength(260);
        builder.Property(s => s.RecordingContentType).HasMaxLength(128);

        // §8 — live broadcast stream URLs (manual stub provider).
        builder.Property(s => s.LiveStreamUrl).HasMaxLength(1024);
        builder.Property(s => s.LiveSignLanguageUrl).HasMaxLength(1024);

        // P5 — D-439: AI live-caption text (manual stub provider, bilingual). 2048
        // matches the Description column — the SSOT the CP form + validator align to.
        builder.Property(s => s.LiveCaptions).HasMaxLength(2048);
        builder.Property(s => s.LiveCaptionsArabic).HasMaxLength(2048);

        builder.HasIndex(s => s.Code).IsUnique();

        // Real DB FK to Hall — same context. Restrict matches the
        // soft-delete policy (admins deactivate halls; they never
        // hard-delete a hall a session points at).
        builder.HasOne(s => s.Hall)
            .WithMany()
            .HasForeignKey(s => s.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // B9b — D-226: optional real FK to the dynamic SessionCategory lookup.
        // Restrict (a category cannot be hard-deleted while a session points at
        // it; admins soft-delete via IsActive). HasForeignKey creates the index.
        builder.HasOne(s => s.Category)
            .WithMany()
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Two query indexes the agenda screen + the operator views ride.
        builder.HasIndex(s => new { s.IsActive, s.StartUtc });
        builder.HasIndex(s => new { s.HallId, s.StartUtc });

        // P3.2 — D-231: the Committee's lifecycle queue lists sessions by
        // status (e.g. the Recorded ones awaiting publish), most-recent first.
        // Status is stored as int (enum, by convention); no HasDefaultValue —
        // the service always writes an explicit value (avoids the EF "0 looks
        // unset" default-backfill trap), and the migration backfills existing
        // rows to Scheduled (0).
        builder.HasIndex(s => new { s.Status, s.StartUtc });
    }
}

/// <summary>P4.1 — D-237: SessionSummary (محضر) configuration. One summary per
/// session (unique <c>SessionId</c>), cascade-deleted with its session. Every
/// HasMaxLength here is the single source of truth the edit form + validator
/// align to (§7); the full-text columns match the News body length (8000).</summary>
internal sealed class SessionSummaryConfiguration
    : IEntityTypeConfiguration<SessionSummary>
{
    public void Configure(EntityTypeBuilder<SessionSummary> builder)
    {
        // D-611 (Wave B) — approval cannot precede review submission.
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

        // B9 — D-225: speaker/host role on the join (stored as int).
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
