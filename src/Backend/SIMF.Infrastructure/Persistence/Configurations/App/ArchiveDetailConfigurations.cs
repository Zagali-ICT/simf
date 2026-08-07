using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Archive;
using SIMF.Domain.Common;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF config for the archive edition's gallery items. Owned
/// snapshot children: parent FK only, cascade-deleted with the edition, no own
/// IsActive (visibility inherits the parent). The FK index doubles as the public
/// read index (WHERE ArchiveEditionId = @id ORDER BY DisplayOrder).</summary>
internal sealed class ArchiveMediaItemConfiguration
    : IEntityTypeConfiguration<ArchiveMediaItem>
{
    public void Configure(EntityTypeBuilder<ArchiveMediaItem> builder)
    {
        builder.ToTable("ArchiveMediaItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Kind).HasConversion<int>().IsRequired();
        builder.Property(item => item.Url).HasMaxLength(512).IsRequired();
        builder.Property(item => item.CaptionEn).HasMaxLength(256);
        builder.Property(item => item.CaptionAr).HasMaxLength(256);

        builder.HasOne(item => item.Edition)
            .WithMany(edition => edition.Media)
            .HasForeignKey(item => item.ArchiveEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.ArchiveEditionId, item.DisplayOrder });
    }
}

/// <summary>EF config for the archive edition's session titles. Same
/// owned-snapshot pattern as <see cref="ArchiveMediaItemConfiguration"/>.</summary>
internal sealed class ArchiveSessionTitleConfiguration
    : IEntityTypeConfiguration<ArchiveSessionTitle>
{
    public void Configure(EntityTypeBuilder<ArchiveSessionTitle> builder)
    {
        builder.ToTable("ArchiveSessionTitles");
        builder.HasKey(title => title.Id);

        builder.Property(title => title.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(title => title.TitleAr).HasMaxLength(200).IsRequired();

        builder.HasOne(title => title.Edition)
            .WithMany(edition => edition.SessionTitles)
            .HasForeignKey(title => title.ArchiveEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(title => new { title.ArchiveEditionId, title.DisplayOrder });
    }
}

/// <summary>EF config for the archive edition's past speakers. Same
/// owned-snapshot pattern; the photo is a relative path (≤256), never absolute.</summary>
internal sealed class ArchivePastSpeakerConfiguration
    : IEntityTypeConfiguration<ArchivePastSpeaker>
{
    public void Configure(EntityTypeBuilder<ArchivePastSpeaker> builder)
    {
        builder.ToTable("ArchivePastSpeakers");
        builder.HasKey(speaker => speaker.Id);

        builder.Property(speaker => speaker.NameEn).HasMaxLength(128).IsRequired();
        builder.Property(speaker => speaker.NameAr).HasMaxLength(128).IsRequired();
        builder.Property(speaker => speaker.PhotoRelativePath).HasMaxLength(256);

        builder.HasOne(speaker => speaker.Edition)
            .WithMany(edition => edition.PastSpeakers)
            .HasForeignKey(speaker => speaker.ArchiveEditionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Logical FK to the Country lookup (no nav, Restrict), mirroring
        // the live Speaker's country FK; the FK auto-creates the index.
        builder.HasOne<Country>()
            .WithMany()
            .HasForeignKey(speaker => speaker.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(speaker => new { speaker.ArchiveEditionId, speaker.DisplayOrder });
    }
}
