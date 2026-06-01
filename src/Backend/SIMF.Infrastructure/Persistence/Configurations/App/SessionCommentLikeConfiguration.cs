using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.SessionComments;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>B5 — D-223: SessionCommentLike join configuration. Composite PK
/// <c>(CommentId, UserId)</c> = per-user-once uniqueness + idempotency guard
/// (copies the SessionModerator shape). Real FK to SessionComment (same
/// context, Cascade so deleting a comment drops its likes); UserId is a
/// logical FK to SimfUser on the Identity DB (no SQL constraint — cross-DB).
/// </summary>
internal sealed class SessionCommentLikeConfiguration
    : IEntityTypeConfiguration<SessionCommentLike>
{
    public void Configure(EntityTypeBuilder<SessionCommentLike> builder)
    {
        builder.ToTable("SessionCommentLikes");
        builder.HasKey(like => new { like.CommentId, like.UserId });

        builder.HasOne(like => like.Comment)
            .WithMany()
            .HasForeignKey(like => like.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // "comments this user liked" lookup (feed LikedByMe projection).
        builder.HasIndex(like => like.UserId);
    }
}
