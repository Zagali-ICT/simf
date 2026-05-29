using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Speaker entity configuration — basic shape as applied by the
/// AddSpeakers migration. Enhanced in Phase C with CountryId FK + Arabic
/// counterparts + consent + social URLs.</summary>
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
        builder.Property(speaker => speaker.CountryCode).HasMaxLength(2);
        builder.Property(speaker => speaker.Bio).HasMaxLength(2048);
        builder.Property(speaker => speaker.BioArabic).HasMaxLength(2048);
        builder.Property(speaker => speaker.Qualifications).HasMaxLength(1024);
        builder.Property(speaker => speaker.TrainingExperience).HasMaxLength(1024);
        builder.Property(speaker => speaker.Awards).HasMaxLength(1024);
        builder.Property(speaker => speaker.PhotoRelativePath).HasMaxLength(256);

        builder.HasIndex(speaker => speaker.Code).IsUnique();
        builder.HasIndex(speaker => new { speaker.IsActive, speaker.DisplayOrder });
    }
}
