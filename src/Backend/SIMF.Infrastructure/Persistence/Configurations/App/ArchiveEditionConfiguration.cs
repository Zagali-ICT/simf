using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Files;
using SIMF.Domain.Archive;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>ArchiveEdition configuration. Guid PK, bilingual title +
/// optional bilingual summary, optional cover image path, three stat counters,
/// soft-delete. Year is unique (one edition per year) and indexed for the
/// default descending sort. Lengths and ranges mirror the FluentValidation
/// validators in <c>AdminArchiveEditionValidators</c> and the service-side
/// <c>AdminArchiveService.Validate</c> exactly.</summary>
internal sealed class ArchiveEditionConfiguration
    : IEntityTypeConfiguration<ArchiveEdition>
{
    public void Configure(EntityTypeBuilder<ArchiveEdition> builder)
    {
        // Two rules the write paths already enforce twice over — the
        // FluentValidation validators (InclusiveBetween(2000, 2100),
        // GreaterThanOrEqualTo(0)) and AdminArchiveService.Validate — but which
        // the table itself did not carry, so anything reaching the context
        // directly could still land a year of 12 or a negative head count.
        // Year range and counter sign are two separate facts, so they get two
        // named constraints rather than one combined predicate (the combined
        // shape is right for MediaPartners, where a coordinate pair is one fact).
        builder.ToTable("ArchiveEditions", table =>
        {
            table.HasCheckConstraint(
                "CK_ArchiveEditions_YearRange",
                "[Year] >= 2000 AND [Year] <= 2100");
            table.HasCheckConstraint(
                "CK_ArchiveEditions_CountersNonNegative",
                "[Attendees] >= 0 AND [Sessions] >= 0 AND [Speakers] >= 0");
        });
        builder.HasKey(edition => edition.Id);

        builder.Property(edition => edition.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(edition => edition.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(edition => edition.SummaryEn).HasMaxLength(1024);
        builder.Property(edition => edition.SummaryAr).HasMaxLength(1024);
        // The cover image, in the one file store. Restrict: deleting a file must never
        // delete the row that shows it.
        builder.HasIndex(edition => edition.CoverImageFileId);
        builder.HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(edition => edition.CoverImageFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // §9 (screen 24-01) — place + date label for the edition detail.
        builder.Property(edition => edition.LocationEn).HasMaxLength(256);
        builder.Property(edition => edition.LocationAr).HasMaxLength(256);
        builder.Property(edition => edition.DateLabelEn).HasMaxLength(128);
        builder.Property(edition => edition.DateLabelAr).HasMaxLength(128);

        // One edition per calendar year — enforced at the DB level as well as
        // in the service (which maps the violation to a 409).
        builder.HasIndex(edition => edition.Year).IsUnique();

        // Public list is ordered by year descending and filtered to active.
        builder.HasIndex(edition => new { edition.IsActive, edition.Year });
    }
}
