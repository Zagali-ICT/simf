using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Cms;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>ContentBlock EF config. Unique
/// <see cref="ContentBlock.Key"/> index — the client-facing slug must
/// resolve to exactly one row.</summary>
internal sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Key).HasMaxLength(128).IsRequired();
        builder.Property(b => b.Content).HasMaxLength(8000).IsRequired();
        builder.Property(b => b.ContentArabic).HasMaxLength(8000).IsRequired();

        builder.HasIndex(b => b.Key).IsUnique();
        builder.HasIndex(b => new { b.IsActive, b.LastUpdatedAt });
    }
}

/// <summary>Banner EF config. Window index covers the public
/// "active banners now" query.</summary>
internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        // A banner display window must end after it starts.
        builder.ToTable("Banners", table => table.HasCheckConstraint(
            "CK_Banners_TimeWindow", "[End] > [Start]"));
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).HasMaxLength(256).IsRequired();
        builder.Property(b => b.TitleArabic).HasMaxLength(256).IsRequired();
        builder.Property(b => b.Body).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.BodyArabic).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.ImageUrl).HasMaxLength(1024);
        builder.Property(b => b.LinkUrl).HasMaxLength(1024);

        builder.HasIndex(b => new { b.IsActive, b.Start, b.End, b.DisplayOrder });
    }
}
