using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>B9b — D-226: SessionCategory lookup configuration. Bilingual,
/// soft-delete via IsActive, ordered. Auto-discovered by
/// <c>ApplyConfigurationsFromAssembly</c> on the <c>...Configurations.App</c>
/// namespace.</summary>
internal sealed class SessionCategoryConfiguration
    : IEntityTypeConfiguration<SessionCategory>
{
    public void Configure(EntityTypeBuilder<SessionCategory> builder)
    {
        builder.ToTable("SessionCategories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name).HasMaxLength(128).IsRequired();
        builder.Property(category => category.NameArabic).HasMaxLength(128).IsRequired();

        // The picker / grid read: active categories ordered by DisplayOrder.
        builder.HasIndex(category => new { category.IsActive, category.DisplayOrder });
    }
}
