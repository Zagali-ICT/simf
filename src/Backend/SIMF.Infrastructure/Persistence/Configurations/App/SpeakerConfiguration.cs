using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-153 / D-157 — Speaker entity configuration.
/// <c>CountryId</c> is a real DB-enforced FK to <c>Country.Id</c>
/// (same DbContext, same physical database — App DB).
/// <c>UserProfileId</c> remains a cross-DB logical FK because
/// <c>UserProfile</c> lives in <c>SIMF_Identity</c> (no cross-database
/// FK constraint syntax in SQL Server).</summary>
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
        // D-159 — real DB FK on the same-DB reference. OnDelete=Restrict
        // matches the soft-delete policy (admins deactivate countries via
        // IsActive=false; they never hard-delete a row a speaker points at).
        // The HasForeignKey call creates the FK index automatically, so the
        // explicit HasIndex(speaker.CountryId) is no longer needed.
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(speaker => speaker.CountryId)
            .OnDelete(DeleteBehavior.Restrict);
        // UserProfileId stays a loose index — UserProfile lives in the
        // Identity DB so a DB-level FK is not possible (D-157).
        builder.HasIndex(speaker => speaker.UserProfileId);
        builder.HasIndex(speaker => new { speaker.IsActive, speaker.DisplayOrder });
    }
}
