using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Badges;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary><see cref="BadgeBatchItem"/> entity configuration — the per-profile-type
/// breakdown of a bulk-badge order, which replaced the rendered
/// <c>CountsSummary</c> string on <c>BadgeBatches</c>.
///
/// Both foreign keys are intra-App (<c>BadgeBatches</c> and <c>ProfileTypes</c>
/// both live on <c>SimfAppDbContext</c>), so the no-cross-database-FK rule is not
/// engaged.
///
/// Lives in the <c>...Configurations.App</c> namespace so
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly</c> picks it up automatically — no manual
/// registration needed.</summary>
internal sealed class BadgeBatchItemConfiguration : IEntityTypeConfiguration<BadgeBatchItem>
{
    public void Configure(EntityTypeBuilder<BadgeBatchItem> builder)
    {
        builder.ToTable("BadgeBatchItems");
        builder.HasKey(item => item.Id);

        // Cascade: a line is meaningless without its order. Orders are only ever
        // soft-deleted (revoke clears IsActive), so in practice this never fires —
        // it is here so a hard delete cannot leave orphan lines behind.
        builder.HasOne(item => item.BadgeBatch)
            .WithMany(batch => batch.Items)
            .HasForeignKey(item => item.BadgeBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, matching how every sibling lookup is referenced: a profile
        // type a historical order counted must not be deletable out from under
        // the breakdown that names it — that name is now read live, so losing the
        // row would blank the label rather than merely date it.
        builder.HasOne<UserProfileType>()
            .WithMany()
            .HasForeignKey(item => item.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // The batches list reads every line of the page's orders in one query and
        // renders them in entry order.
        builder.HasIndex(item => new { item.BadgeBatchId, item.DisplayOrder });
    }
}
