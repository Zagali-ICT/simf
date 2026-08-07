using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SessionQuestion entity configuration.
/// Real FK to Session (same DbContext). SubmittedByUserId is a logical
/// FK to SimfUser on the Identity DB. Query index supports the
/// moderator queue scan: `(SessionId, IsHidden, Order)`.</summary>
internal sealed class SessionQuestionConfiguration : IEntityTypeConfiguration<SessionQuestion>
{
    public void Configure(EntityTypeBuilder<SessionQuestion> builder)
    {
        builder.ToTable("SessionQuestions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionText).HasMaxLength(1000).IsRequired();

        // Q&A pipeline columns. No HasDefaultValue on Status — the
        // service always writes an explicit value (the migration backfills
        // existing rows). Phase/Status stored as int by convention.
        builder.Property(q => q.AiFilterVerdict).HasMaxLength(256);
        builder.Property(q => q.AssignedToRole).HasMaxLength(64);

        // Real DB FK to Session — same context. Cascade so a Session
        // delete (rare; usually deactivate) takes its questions with
        // it. Reorder + hide stay on the row, not the cascade path.
        builder.HasOne(q => q.Session)
            .WithMany()
            .HasForeignKey(q => q.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.SessionId, q.IsHidden, q.Order });
        builder.HasIndex(q => q.SubmittedByUserId);
        // The moderator desk lists the Committee-approved set per
        // session; the Committee queue scans Pending across sessions.
        builder.HasIndex(q => new { q.SessionId, q.Status, q.Order });
        builder.HasIndex(q => new { q.Status, q.CreatedAt });
    }
}

/// <summary>SessionModerator join configuration.
/// Composite PK (SessionId, UserId). Cascade from Session so removing
/// the session drops the grant set; UserId stays a logical FK to
/// Identity DB (no SQL constraint).</summary>
internal sealed class SessionModeratorConfiguration : IEntityTypeConfiguration<SessionModerator>
{
    public void Configure(EntityTypeBuilder<SessionModerator> builder)
    {
        builder.ToTable("SessionModerators");
        builder.HasKey(m => new { m.SessionId, m.UserId });

        builder.HasOne(m => m.Session)
            .WithMany()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The moderator lookup happens once per moderator request — a
        // simple UserId index covers the "list every session this user
        // moderates" query for the CP moderation desk landing page.
        builder.HasIndex(m => m.UserId);
    }
}
