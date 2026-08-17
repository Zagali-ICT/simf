using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SessionQuestion entity configuration.
/// Real FK to Session (same DbContext). SubmittedByUserId is a logical
/// FK to SimfUser on the Identity DB. The three query indexes each answer one
/// read: the public recorded archive, the moderator desk, the Committee
/// queue.</summary>
internal sealed class SessionQuestionConfiguration : IEntityTypeConfiguration<SessionQuestion>
{
    public void Configure(EntityTypeBuilder<SessionQuestion> builder)
    {
        builder.ToTable("SessionQuestions", table =>
        {
            // Escalation is written as a set of three by the Committee's
            // EscalateAsync, and nothing ever clears it. All three null means
            // "never escalated"; any partial combination is an escalation whose
            // role, actor or time is missing, which no reader can interpret.
            table.HasCheckConstraint(
                "CK_SessionQuestions_EscalationTrio",
                "([AssignedToRole] IS NULL AND [EscalatedByUserId] IS NULL "
                + "AND [EscalatedAt] IS NULL) "
                + "OR ([AssignedToRole] IS NOT NULL AND [EscalatedByUserId] IS NOT NULL "
                + "AND [EscalatedAt] IS NOT NULL)");
            // The on-stage marker and its timestamp move together: a push stamps
            // both, and hiding (from either the desk or the Committee) clears
            // both. A pushed row with no PushedAt would sort into the recorded
            // archive with no time to show against it.
            table.HasCheckConstraint(
                "CK_SessionQuestions_PushedPair",
                "([IsPushed] = 0 AND [PushedAt] IS NULL) "
                + "OR ([IsPushed] = 1 AND [PushedAt] IS NOT NULL)");
        });
        builder.HasKey(q => q.Id);

        builder.Property(q => q.QuestionText).HasMaxLength(1000).IsRequired();

        // Visibility lives on Status alone, so the IsHidden column is dropped
        // from the model: nothing had written it since the moderation desk moved
        // over, and every reader that still quoted it read false on a genuinely
        // hidden row. Unmapped rather than deleted outright only because the last
        // remaining assignment sits in a file another lane owns; the property and
        // this call go together once that line is gone.
        builder.Ignore(q => q.IsHidden);

        // Q&A pipeline columns. No HasDefaultValue on Status — the service
        // always writes an explicit value on every create path.
        // Phase/Status stored as int by convention.
        builder.Property(q => q.AiFilterVerdict).HasMaxLength(256);
        builder.Property(q => q.AssignedToRole).HasMaxLength(64);

        // Real DB FK to Session — same context. Cascade so a Session
        // delete (rare; usually deactivate) takes its questions with
        // it. Reorder + hide stay on the row, not the cascade path.
        builder.HasOne(q => q.Session)
            .WithMany()
            .HasForeignKey(q => q.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // The public recorded archive reads the questions a moderator pushed on
        // stage, in queue order: WHERE SessionId AND IsPushed ORDER BY Order.
        // This index used to carry IsHidden in that middle slot, which no writer
        // has set since visibility moved onto Status -- a constant column wide,
        // seeking nothing. There is no index on SubmittedByUserId either: it is
        // projected onto every question row but no query filters or sorts by it.
        builder.HasIndex(q => new { q.SessionId, q.IsPushed, q.Order });
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
