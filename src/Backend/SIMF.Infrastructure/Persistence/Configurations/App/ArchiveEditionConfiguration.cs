using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Archive;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-199 — ArchiveEdition configuration. Guid PK, bilingual title +
/// optional bilingual summary, optional cover image path, three stat counters,
/// soft-delete. Year is unique (one edition per year) and indexed for the
/// default descending sort. Lengths mirror the FluentValidation validators
/// in <c>ArchiveEditionValidators</c> exactly.</summary>
internal sealed class ArchiveEditionConfiguration
    : IEntityTypeConfiguration<ArchiveEdition>
{
    public void Configure(EntityTypeBuilder<ArchiveEdition> builder)
    {
        builder.ToTable("ArchiveEditions");
        builder.HasKey(edition => edition.Id);

        builder.Property(edition => edition.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(edition => edition.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(edition => edition.SummaryEn).HasMaxLength(1024);
        builder.Property(edition => edition.SummaryAr).HasMaxLength(1024);
        builder.Property(edition => edition.CoverImageRelativePath).HasMaxLength(512);

        // One edition per calendar year — enforced at the DB level as well as
        // in the service (which maps the violation to a 409).
        builder.HasIndex(edition => edition.Year).IsUnique();

        // Public list is ordered by year descending and filtered to active.
        builder.HasIndex(edition => new { edition.IsActive, edition.Year });
    }
}
