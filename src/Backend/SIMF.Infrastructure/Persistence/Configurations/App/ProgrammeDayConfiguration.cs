using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>ProgrammeDay configuration: one programme day per
/// <c>Date</c>, with a bilingual title, ordered by <c>DisplayOrder</c> then
/// <c>Date</c>. The day's logo/banner lives in the unified Asset table
/// (<c>ProgrammeDayImage</c>, owner = <c>Id</c>) — no image column here.
/// One active day per date is enforced at the service layer (a soft-deleted day
/// must not block re-creating the same date).</summary>
internal sealed class ProgrammeDayConfiguration
    : IEntityTypeConfiguration<ProgrammeDay>
{
    public void Configure(EntityTypeBuilder<ProgrammeDay> builder)
    {
        builder.ToTable("ProgrammeDays");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).HasMaxLength(128).IsRequired();
        builder.Property(d => d.TitleArabic).HasMaxLength(128).IsRequired();

        // The agenda + the CP list both order by DisplayOrder then Date,
        // filtered to the active rows.
        builder.HasIndex(d => new { d.IsActive, d.DisplayOrder, d.Date });

        // One ACTIVE programme day per date (a soft-deleted day
        // must not block re-creating the same date, hence the filter).
        builder.HasIndex(d => d.Date)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
