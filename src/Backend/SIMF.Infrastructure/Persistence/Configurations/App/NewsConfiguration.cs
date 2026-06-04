using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.PublicRelations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 — EF mapping for <see cref="News"/>. Lives in the
/// <c>SIMF.Infrastructure.Persistence.Configurations.App</c> namespace so it is
/// picked up automatically by <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly(..., type =&gt; type.Namespace == "...App")</c>
/// filter — no manual <c>ApplyConfiguration</c> call is needed.
/// <para>
/// Every <c>HasMaxLength</c> here is the single source of truth that the
/// FluentValidation <c>MaximumLength</c> rules on the create / update endpoints
/// mirror exactly (SIMF rule: validation length == EF length).
/// </para></summary>
internal sealed class NewsConfiguration : IEntityTypeConfiguration<News>
{
    public void Configure(EntityTypeBuilder<News> builder)
    {
        builder.ToTable("News");
        builder.HasKey(news => news.Id);

        builder.Property(news => news.Title).HasMaxLength(200).IsRequired();
        builder.Property(news => news.TitleArabic).HasMaxLength(200).IsRequired();

        builder.Property(news => news.Excerpt).HasMaxLength(500);
        builder.Property(news => news.ExcerptArabic).HasMaxLength(500);

        builder.Property(news => news.Body).HasMaxLength(8000).IsRequired();
        builder.Property(news => news.BodyArabic).HasMaxLength(8000).IsRequired();

        builder.Property(news => news.Category).HasMaxLength(100).IsRequired();
        builder.Property(news => news.CategoryArabic).HasMaxLength(100).IsRequired();

        builder.Property(news => news.ImageRelativePath).HasMaxLength(512);

        builder.Property(news => news.PublishedAt).IsRequired();
        builder.Property(news => news.CreatedAt).IsRequired();

        // The public feed filters on IsActive and orders by PublishedAt desc;
        // this composite keeps that hot query index-covered. The admin grid
        // also orders by PublishedAt so it benefits too.
        builder.HasIndex(news => new { news.IsActive, news.PublishedAt });
    }
}
