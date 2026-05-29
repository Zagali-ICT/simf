using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>
/// EF configuration for the <see cref="Interest"/> lookup (P9 — D-050).
/// D-167 moved this onto <c>SimfAppDbContext</c>. Length caps line up
/// with the <c>AdminCreateInterestRequestValidator</c> rules. A composite
/// filter index on <c>(IsActive, DisplayOrder)</c> matches the
/// visitor-picker query shape.
/// </summary>
internal sealed class InterestConfiguration : IEntityTypeConfiguration<Interest>
{
    public void Configure(EntityTypeBuilder<Interest> builder)
    {
        builder.ToTable("Interests");
        builder.HasKey(interest => interest.Id);

        builder.Property(interest => interest.Name)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(interest => interest.NameArabic)
            .HasMaxLength(128)
            .IsRequired();

        // Name is unique within the lookup so the CP cannot create a
        // duplicate row by accident.
        builder.HasIndex(interest => interest.Name).IsUnique();

        // The visitor picker filters by IsActive + orders by DisplayOrder;
        // the index supports that read.
        builder.HasIndex(interest => new { interest.IsActive, interest.DisplayOrder });
    }
}
