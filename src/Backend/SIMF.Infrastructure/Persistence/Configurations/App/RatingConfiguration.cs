using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Feedback;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF mapping for the dynamic, config-driven rating model. Replaces the
/// old fixed single-row <c>Rating</c> table. Real DB FKs within this DbContext;
/// <c>RatingResponse.UserId</c> and <c>RatingResponse.TargetId</c> stay bare
/// Guids (cross-DB to Identity / polymorphic target — the D-157 separation rule).
/// Mirrors <c>FaqConfiguration</c> for the parent → child cascade shape.</summary>
internal sealed class RatingTypeConfiguration : IEntityTypeConfiguration<RatingType>
{
    public void Configure(EntityTypeBuilder<RatingType> builder)
    {
        builder.ToTable("RatingTypes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(128).IsRequired();
        builder.Property(t => t.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(t => t.CommentLabel).HasMaxLength(128);
        builder.Property(t => t.CommentLabelArabic).HasMaxLength(128);

        // Stable slug the app + worker resolve by — unique across types.
        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => new { t.IsActive, t.DisplayOrder });

        builder.HasMany(t => t.Groups)
            .WithOne(g => g.Type)
            .HasForeignKey(g => g.RatingTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Questions)
            .WithOne(q => q.Type)
            .HasForeignKey(q => q.RatingTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RatingQuestionGroupConfiguration : IEntityTypeConfiguration<RatingQuestionGroup>
{
    public void Configure(EntityTypeBuilder<RatingQuestionGroup> builder)
    {
        builder.ToTable("RatingQuestionGroups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name).HasMaxLength(128).IsRequired();
        builder.Property(g => g.NameArabic).HasMaxLength(128).IsRequired();

        builder.HasIndex(g => new { g.RatingTypeId, g.IsActive, g.DisplayOrder });

        // NoAction (not SetNull/Cascade) so RatingQuestions has a single cascade
        // path from RatingType — SQL Server rejects "multiple cascade paths" when a
        // table is reachable by cascade both directly and via the group. The app
        // soft-deletes groups (IsActive=false) and the form service renders a
        // question whose group is inactive as flat/ungrouped, so this FK behaviour
        // is never exercised at runtime; the only owner that cascade-deletes a
        // question is its RatingType.
        builder.HasMany(g => g.Questions)
            .WithOne(q => q.Group)
            .HasForeignKey(q => q.RatingQuestionGroupId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class RatingQuestionConfiguration : IEntityTypeConfiguration<RatingQuestion>
{
    public void Configure(EntityTypeBuilder<RatingQuestion> builder)
    {
        builder.ToTable("RatingQuestions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Text).HasMaxLength(512).IsRequired();
        builder.Property(q => q.TextArabic).HasMaxLength(512).IsRequired();

        builder.HasIndex(q => new { q.RatingTypeId, q.IsActive, q.DisplayOrder });
    }
}

internal sealed class RatingResponseConfiguration : IEntityTypeConfiguration<RatingResponse>
{
    public void Configure(EntityTypeBuilder<RatingResponse> builder)
    {
        // D-611 (Wave B) — the optional overall score is 1–5 when present.
        builder.ToTable("RatingResponses", table => table.HasCheckConstraint(
            "CK_RatingResponses_OverallStars",
            "[OverallStars] IS NULL OR [OverallStars] BETWEEN 1 AND 5"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.TargetId).IsRequired();

        // Comment max length MUST stay aligned with the FluentValidation
        // MaximumLength(2000) on SubmitRatingRequest and any UI MaxLength.
        builder.Property(r => r.Comment).HasMaxLength(2000);

        // Real FK to the type (block a hard delete of a type that has responses).
        builder.HasOne(r => r.Type)
            .WithMany()
            .HasForeignKey(r => r.RatingTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Answers)
            .WithOne(a => a.Response)
            .HasForeignKey(a => a.RatingResponseId)
            .OnDelete(DeleteBehavior.Cascade);

        // One submission per user per (type, target). TargetId is Guid.Empty for
        // global types so the composite index stays uniform (SQL Server treats
        // NULLs as distinct, which would let a user rate "App" many times).
        builder.HasIndex(r => new { r.UserId, r.RatingTypeId, r.TargetId }).IsUnique();
    }
}

internal sealed class RatingAnswerConfiguration : IEntityTypeConfiguration<RatingAnswer>
{
    public void Configure(EntityTypeBuilder<RatingAnswer> builder)
    {
        // D-611 (Wave B) — a per-question score is between 1 and 5.
        builder.ToTable("RatingAnswers", table => table.HasCheckConstraint(
            "CK_RatingAnswers_Stars", "[Stars] BETWEEN 1 AND 5"));
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Stars).IsRequired();

        // Real FK to the question (block a hard delete of an answered question).
        builder.HasOne(a => a.Question)
            .WithMany()
            .HasForeignKey(a => a.RatingQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // A question is scored at most once per submission.
        builder.HasIndex(a => new { a.RatingResponseId, a.RatingQuestionId }).IsUnique();
    }
}
