using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Media;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 (Mockup page 30) — <see cref="MediaItem"/> configuration.
/// Lives in the <c>...Configurations.App</c> namespace so it is picked up by
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly</c> namespace filter. Binary bytes are
/// out-of-row (D-90), so only the relative-path strings are persisted here.
/// Max lengths below are the single source of truth the FluentValidation
/// rules and the service validation mirror.</summary>
internal sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("MediaItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Kind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(item => item.Title).HasMaxLength(200);
        builder.Property(item => item.TitleArabic).HasMaxLength(200);

        builder.Property(item => item.ImageRelativePath).HasMaxLength(256);
        builder.Property(item => item.Url).HasMaxLength(2048);
        builder.Property(item => item.ThumbnailRelativePath).HasMaxLength(256);

        builder.Property(item => item.Album).HasMaxLength(200);
        builder.Property(item => item.AlbumArabic).HasMaxLength(200);

        // The public gallery query filters by IsActive then orders by
        // DisplayOrder, optionally narrowed to one album — index matches
        // that access path (mirrors the Speaker (IsActive, DisplayOrder)
        // index, with Album appended for the by-album filter).
        builder.HasIndex(item => new { item.IsActive, item.Album, item.DisplayOrder });

        // D-611 (Wave B) — the active gallery read path filtered by media kind.
        builder.HasIndex(item => new { item.IsActive, item.Kind, item.DisplayOrder });
    }
}
