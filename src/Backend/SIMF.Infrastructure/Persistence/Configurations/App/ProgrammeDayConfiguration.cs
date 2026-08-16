using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>ProgrammeDay configuration: one programme day per
/// <c>Date</c>, with a bilingual title, ordered by <c>DisplayOrder</c> then
/// <c>Date</c>. The day's logo/banner lives in the unified Asset table
/// (<c>ProgrammeDayImage</c>, owner = <c>Id</c>) — no image column here.
/// One ACTIVE day per date is enforced in BOTH places: the filtered unique index
/// below holds it in the database, and <c>AdminProgrammeDayService</c> pre-checks
/// it so the admin gets a bilingual 400 rather than a constraint violation. The
/// filter is what lets a soft-deleted day sit on a date that is being
/// re-created.</summary>
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
