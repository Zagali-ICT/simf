using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Domain.Media;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary><see cref="MediaItem"/> configuration.
/// Lives in the <c>...Configurations.App</c> namespace so it is picked up by
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly</c> namespace filter. Binary bytes are
/// out-of-row in the unified StoredFile store, referenced by the
/// <c>ImageFileId</c> / <c>ThumbnailFileId</c> pointers. Max lengths below are
/// the single source of truth the FluentValidation rules and the service
/// validation mirror.</summary>
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

        builder.Property(item => item.Url).HasMaxLength(2048);

        builder.Property(item => item.Album).HasMaxLength(200);
        builder.Property(item => item.AlbumArabic).HasMaxLength(200);

        // The public gallery query filters by IsActive then orders by
        // DisplayOrder, optionally narrowed to one album — index matches
        // that access path (mirrors the Speaker (IsActive, DisplayOrder)
        // index, with Album appended for the by-album filter).
        builder.HasIndex(item => new { item.IsActive, item.Album, item.DisplayOrder });

        // The active gallery read path filtered by media kind.
        builder.HasIndex(item => new { item.IsActive, item.Kind, item.DisplayOrder });

        // Real foreign keys into the one file store. Both columns were already
        // typed Guids, so this adds the constraint the entity's own doc comment
        // said it did not have.
        //
        // No navigation property, matching UserProfile's file keys: nothing walks
        // from a media item to its file, because the bytes are always fetched
        // through IFileService by id.
        //
        // Restrict, not Cascade: deleting a file must never delete the gallery
        // item that shows it. It should never fire, because StoredFileService
        // deactivates rows rather than removing them, which is precisely what
        // makes the key worth having.
        builder.HasIndex(item => item.ImageFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(item => item.ImageFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.ThumbnailFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(item => item.ThumbnailFileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
