using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>HallAllocation EF config (the flexible
/// whole / random-by-count / row-column reservation of a hall over a time-slot).
/// Real FK to Hall (Restrict). Indexes support the overlap check (active rows in a
/// hall, by purpose) — time-overlap itself is enforced in the service, not a SQL
/// constraint.</summary>
internal sealed class HallAllocationConfiguration : IEntityTypeConfiguration<HallAllocation>
{
    public void Configure(EntityTypeBuilder<HallAllocation> builder)
    {
        // An allocation must end after it starts, and each mode carries exactly its
        // own detail column: RandomByCount(1) a unit count of at least one, RowColumn(2)
        // a CSV spec, Whole(0) neither. CreateAllocationAsync is the ONLY write path
        // (there is no update path) and it already sets each column to null outside its
        // own mode, so these are true backstops rather than new rules. The UnitCount
        // check used to read "[UnitCount] >= 1", which passed silently on NULL and so
        // never caught a count parked on a Whole or RowColumn allocation.
        builder.ToTable("HallAllocations", table =>
        {
            table.HasCheckConstraint(
                "CK_HallAllocations_TimeWindow", "[End] > [Start]");
            table.HasCheckConstraint(
                "CK_HallAllocations_UnitCount",
                "([Mode] = 1 AND [UnitCount] >= 1) OR ([Mode] <> 1 AND [UnitCount] IS NULL)");
            table.HasCheckConstraint(
                "CK_HallAllocations_RowColumnSpec",
                "([Mode] = 2 AND [RowColumnSpec] IS NOT NULL) OR ([Mode] <> 2 AND [RowColumnSpec] IS NULL)");
        });
        builder.HasKey(a => a.Id);

        builder.Property(a => a.RowColumnSpec).HasMaxLength(512);
        builder.Property(a => a.Notes).HasMaxLength(512);
        builder.Property(a => a.Start).IsRequired();
        builder.Property(a => a.End).IsRequired();

        builder.HasOne(a => a.Hall)
            .WithMany()
            .HasForeignKey(a => a.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // Overlap lookups: active allocations in a hall, optionally by purpose.
        builder.HasIndex(a => new { a.HallId, a.Purpose, a.ReleasedAt });
    }
}
