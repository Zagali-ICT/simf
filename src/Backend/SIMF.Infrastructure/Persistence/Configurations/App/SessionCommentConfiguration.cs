using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionComments;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 (Mockup page 28) — SessionComment entity
/// configuration. Real FK to Session (same DbContext); UserId is a
/// logical FK to SimfUser on the Identity DB (no SQL constraint —
/// cross-DB). Query index supports the two hot reads:
/// the public approved-feed scan and the admin moderation list, both
/// keyed on <c>(SessionId, Status, IsActive)</c>.</summary>
internal sealed class SessionCommentConfiguration : IEntityTypeConfiguration<SessionComment>
{
    public void Configure(EntityTypeBuilder<SessionComment> builder)
    {
        builder.ToTable("SessionComments");
        builder.HasKey(c => c.Id);

        // MaximumLength(1000) in the validator path + this HasMaxLength(1000)
        // are kept in lockstep per CLAUDE.md §7 (validation alignment).
        builder.Property(c => c.Body).HasMaxLength(1000).IsRequired();

        // AiFilterVerdict is a short free-text bucket — cap it so a
        // future filter implementation cannot write an unbounded string.
        builder.Property(c => c.AiFilterVerdict).HasMaxLength(64);

        // Persist the moderation status as an int (enum default) — the
        // SessionCommentStatus enum is value-frozen append-only (D-199),
        // so the integer mapping is stable.
        builder.Property(c => c.Status);

        // Real DB FK to Session — same context. Cascade so a Session
        // delete (rare; usually deactivate) takes its comments with it.
        builder.HasOne(c => c.Session)
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Covers both the public approved-feed scan
        // (SessionId + Status=Approved + IsActive) and the admin
        // moderation list (SessionId + IsActive, any status).
        builder.HasIndex(c => new { c.SessionId, c.Status, c.IsActive });

        // The "comments by this user" lookup (moderation / abuse review).
        builder.HasIndex(c => c.UserId);
    }
}
