using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Cms;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-173 (gap doc G8) — ContentBlock EF config. Unique
/// <see cref="ContentBlock.Key"/> index — the client-facing slug must
/// resolve to exactly one row.</summary>
internal sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Key).HasMaxLength(128).IsRequired();
        builder.Property(b => b.ContentEn).HasMaxLength(8000).IsRequired();
        builder.Property(b => b.ContentAr).HasMaxLength(8000).IsRequired();

        builder.HasIndex(b => b.Key).IsUnique();
        builder.HasIndex(b => new { b.IsActive, b.LastUpdatedAt });
    }
}

/// <summary>D-173 — Banner EF config. Window index covers the public
/// "active banners now" query.</summary>
internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("Banners");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.TitleEn).HasMaxLength(256).IsRequired();
        builder.Property(b => b.TitleAr).HasMaxLength(256).IsRequired();
        builder.Property(b => b.BodyEn).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.BodyAr).HasMaxLength(2000).IsRequired();
        builder.Property(b => b.ImageUrl).HasMaxLength(1024);
        builder.Property(b => b.LinkUrl).HasMaxLength(1024);

        builder.HasIndex(b => new { b.IsActive, b.StartUtc, b.EndUtc, b.DisplayOrder });
    }
}
