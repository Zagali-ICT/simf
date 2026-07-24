using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// D-134 Sprint B — Theme entity configuration (D-135 freeze-lift; first
/// new app-side table). EF HasMaxLength is the length source of truth;
/// server-side required + length validation is enforced in the admin service
/// layer (AdminThemeService, bilingual coded errors), not via FluentValidation
/// (SIMF-SES-001 §7 alignment). Unique index on <c>Code</c>.
/// </summary>
internal sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("Themes");
        builder.HasKey(theme => theme.Id);

        builder.Property(theme => theme.Code).HasMaxLength(16).IsRequired();
        builder.Property(theme => theme.Name).HasMaxLength(128).IsRequired();
        builder.Property(theme => theme.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(theme => theme.Description).HasMaxLength(1024);
        builder.Property(theme => theme.DescriptionArabic).HasMaxLength(1024);
        builder.Property(theme => theme.PageColor).HasMaxLength(32).IsRequired();

        builder.HasIndex(theme => theme.Code).IsUnique();
        builder.HasIndex(theme => new { theme.IsActive, theme.DisplayOrder });
    }
}
