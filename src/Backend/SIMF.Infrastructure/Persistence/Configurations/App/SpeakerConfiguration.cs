using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Common;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Speaker entity configuration.
/// <c>CountryId</c> is a real DB-enforced FK to <c>Country.Id</c>
/// (same DbContext, same physical database — App DB).
/// <c>UserProfileId</c> is a real DB FK too, because
/// <c>UserProfile</c> lives on <c>SimfAppDbContext</c>.</summary>
internal sealed class SpeakerConfiguration : IEntityTypeConfiguration<Speaker>
{
    public void Configure(EntityTypeBuilder<Speaker> builder)
    {
        // The contact card's coordinate is a pair or nothing, and when set it is a
        // real coordinate. Mirrors the range + pairing rules AdminSpeakerService
        // already enforces, and the same shape as CK_Halls_Geofence.
        builder.ToTable("Speakers", table =>
        {
            table.HasCheckConstraint(
                "CK_Speakers_Location",
                "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] >= -90 AND [Latitude] <= 90 AND [Longitude] >= -180 AND [Longitude] <= 180)");
            // Zero or positive, the same rule AdminSpeakerService already refuses a
            // negative order with (400 SPEAKER_INVALID). Held here too so a seed or
            // a repair script cannot store a speaker that sorts ahead of every
            // hand-ordered one. Same shape as CK_Halls_Capacity.
            table.HasCheckConstraint("CK_Speakers_DisplayOrder", "[DisplayOrder] >= 0");
        });
        builder.HasKey(speaker => speaker.Id);

        builder.Property(speaker => speaker.Code).HasMaxLength(16).IsRequired();
        builder.Property(speaker => speaker.Name).HasMaxLength(128).IsRequired();
        builder.Property(speaker => speaker.NameArabic).HasMaxLength(128).IsRequired();
        // Owner 2026-07-19 — widened 64 → 256: the Arabic rank/title runs longer
        // than the English (live data reaches ~120 chars); English widened too for
        // symmetry. Matches the CP form MaxLength (the EF/UI length lock).
        builder.Property(speaker => speaker.Rank).HasMaxLength(256);
        builder.Property(speaker => speaker.RankArabic).HasMaxLength(256);

        builder.Property(speaker => speaker.Bio).HasMaxLength(2048);
        builder.Property(speaker => speaker.BioArabic).HasMaxLength(2048);
        builder.Property(speaker => speaker.Qualifications).HasMaxLength(1024);
        builder.Property(speaker => speaker.QualificationsArabic).HasMaxLength(1024);
        builder.Property(speaker => speaker.TrainingExperience).HasMaxLength(1024);
        builder.Property(speaker => speaker.TrainingExperienceArabic).HasMaxLength(1024);
        builder.Property(speaker => speaker.Awards).HasMaxLength(1024);
        builder.Property(speaker => speaker.AwardsArabic).HasMaxLength(1024);

        builder.Property(speaker => speaker.FacebookUrl).HasMaxLength(256);
        builder.Property(speaker => speaker.LinkedInUrl).HasMaxLength(256);
        builder.Property(speaker => speaker.XUrl).HasMaxLength(256);
        builder.Property(speaker => speaker.InstagramUrl).HasMaxLength(256);
        builder.Property(speaker => speaker.WebsiteUrl).HasMaxLength(256);

        // Contact identity-card fields inlined from the removed shared Contact
        // directory. Latitude/Longitude are double? and need no length.
        // The Website slot is WebsiteUrl above.
        builder.Property(speaker => speaker.Email).HasMaxLength(320);
        builder.Property(speaker => speaker.PhonePrimary).HasMaxLength(32);
        builder.Property(speaker => speaker.PhoneSecondary).HasMaxLength(32);
        builder.Property(speaker => speaker.City).HasMaxLength(128);
        builder.Property(speaker => speaker.CityArabic).HasMaxLength(128);


        builder.HasIndex(speaker => speaker.Code).IsUnique();
        // Real DB FK on the same-DB reference. OnDelete=Restrict
        // matches the soft-delete policy (admins deactivate countries via
        // IsActive=false; they never hard-delete a row a speaker points at).
        // The HasForeignKey call creates the FK index automatically, so the
        // explicit HasIndex(speaker.CountryId) is no longer needed.
        builder.HasOne(speaker => speaker.Country)
            .WithMany()
            .HasForeignKey(speaker => speaker.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
        // Real FK to UserProfile (now same-DB). Restrict because
        // a speaker keyed to a deactivated user profile should still
        // surface in the public speakers list — admins remove the
        // linkage by clearing the UserProfileId on the speaker, not by
        // hard-deleting the profile.
        builder.HasOne<UserProfile>()
            .WithMany()
            .HasForeignKey(speaker => speaker.UserProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        // At most one speaker row per linked profile. The link is an identity
        // mapping, not a tag: the approved-summary read resolves the caller's
        // profile to a speaker to decide whether they host the session, so two
        // speaker rows claiming one profile would be two records of one person and
        // two answers to that authorization question. FILTERED, because the null
        // is the ordinary case — most speakers are external guests with no SIMF
        // account — and SQL Server treats nulls as equal in a unique index, so
        // without the filter the table would admit exactly one unlinked speaker.
        builder.HasIndex(speaker => speaker.UserProfileId)
            .IsUnique()
            .HasFilter("[UserProfileId] IS NOT NULL");
        builder.HasIndex(speaker => new { speaker.IsActive, speaker.DisplayOrder });
    }
}
