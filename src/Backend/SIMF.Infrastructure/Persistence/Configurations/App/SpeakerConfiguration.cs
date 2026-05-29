using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-153 — Speaker entity configuration. <c>CountryId</c> is a
/// logical FK to <c>Country.Id</c> (same DbContext); <c>UserProfileId</c>
/// is a cross-context logical FK to <c>UserProfile.Id</c> in the Identity
/// context, enforced in application code at write time.</summary>
internal sealed class SpeakerConfiguration : IEntityTypeConfiguration<Speaker>
{
    public void Configure(EntityTypeBuilder<Speaker> builder)
    {
        builder.ToTable("Speakers");
        builder.HasKey(speaker => speaker.Id);

        builder.Property(speaker => speaker.Code).HasMaxLength(16).IsRequired();
        builder.Property(speaker => speaker.Name).HasMaxLength(128).IsRequired();
        builder.Property(speaker => speaker.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(speaker => speaker.Rank).HasMaxLength(64);

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

        builder.Property(speaker => speaker.PhotoRelativePath).HasMaxLength(256);

        builder.HasIndex(speaker => speaker.Code).IsUnique();
        builder.HasIndex(speaker => speaker.CountryId);
        builder.HasIndex(speaker => speaker.UserProfileId);
        builder.HasIndex(speaker => new { speaker.IsActive, speaker.DisplayOrder });
    }
}
