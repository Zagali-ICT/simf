using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Feedback;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 — EF mapping for <see cref="Rating"/> (Mockup screen 40).
/// Mirrors <c>SeatReservationConfiguration</c>: lean per-attendee row with a
/// unique index that enforces one rating per user.</summary>
public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Stars).IsRequired();

        // Per-element scores (Figma "قيّم العناصر") — additive nullable columns;
        // no IsRequired so existing rows backfill as NULL.
        builder.Property(x => x.OrganizationStars);
        builder.Property(x => x.ContentStars);
        builder.Property(x => x.AppStars);
        builder.Property(x => x.VenueStars);

        // Comment max length MUST stay aligned with the FluentValidation
        // MaximumLength(2000) on RateRequest and any UI MaxLength.
        builder.Property(x => x.Comment).HasMaxLength(2000);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        // D-199 — one rating per attendee (upsert target).
        builder.HasIndex(x => x.UserId).IsUnique();
    }
}
