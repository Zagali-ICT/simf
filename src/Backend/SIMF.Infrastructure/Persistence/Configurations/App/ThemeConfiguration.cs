using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// Theme entity configuration — the first new app-side table. EF HasMaxLength
/// is the length source of truth; server-side required + length validation is
/// enforced in the admin service layer (AdminThemeService, bilingual coded
/// errors), not via FluentValidation. Unique index on <c>Code</c>.
/// </summary>
internal sealed class ThemeConfiguration : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        // Zero or positive, the same rule AdminThemeService already refuses a
        // negative order with (400 THEME_INVALID). Held here too so a seed or a
        // repair script cannot store a theme that sorts ahead of every
        // hand-ordered one. Same shape as CK_Halls_Capacity.
        builder.ToTable("Themes", table => table.HasCheckConstraint(
            "CK_Themes_DisplayOrder", "[DisplayOrder] >= 0"));
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
