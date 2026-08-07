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
        // An allocation must end after it starts.
        builder.ToTable("HallAllocations", table => table.HasCheckConstraint(
            "CK_HallAllocations_TimeWindow", "[End] > [Start]"));
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
