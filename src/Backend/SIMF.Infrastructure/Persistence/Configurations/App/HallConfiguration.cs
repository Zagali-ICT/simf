using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-134 Sprint B — Hall entity configuration (D-135 freeze-lift).
/// Mirrors <see cref="ThemeConfiguration"/>; unique index on <c>Code</c>.</summary>
internal sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        builder.ToTable("Halls");
        builder.HasKey(hall => hall.Id);

        builder.Property(hall => hall.Code).HasMaxLength(16).IsRequired();
        builder.Property(hall => hall.Name).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.NameArabic).HasMaxLength(128).IsRequired();
        builder.Property(hall => hall.Floor).HasMaxLength(32);
        builder.Property(hall => hall.EquipmentNotes).HasMaxLength(1024);

        builder.HasIndex(hall => hall.Code).IsUnique();
        builder.HasIndex(hall => new { hall.IsActive, hall.Name });
    }
}
