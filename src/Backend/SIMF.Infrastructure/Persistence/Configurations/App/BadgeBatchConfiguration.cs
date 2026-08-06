using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Badges;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary><see cref="BadgeBatch"/> entity configuration.
/// Soft-delete via IsActive; the member <c>UserProfile</c> rows own the FK back to
/// this table (see <c>UserProfileConfiguration</c>). Indexed on
/// <c>(IsActive, CreatedAt)</c> because the CP batches list orders active batches
/// newest-first.
///
/// Lives in the <c>...Configurations.App</c> namespace so
/// <c>SimfAppDbContext.OnModelCreating</c>'s
/// <c>ApplyConfigurationsFromAssembly</c> picks it up automatically — no manual
/// registration needed.</summary>
internal sealed class BadgeBatchConfiguration : IEntityTypeConfiguration<BadgeBatch>
{
    public void Configure(EntityTypeBuilder<BadgeBatch> builder)
    {
        builder.ToTable("BadgeBatches");
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.CountsSummary).HasMaxLength(512).IsRequired();
        // Matches the organiser-email cap validated in BulkGenerateBadgesAsync.
        builder.Property(batch => batch.RecipientEmail).HasMaxLength(256);

        builder.HasIndex(batch => new { batch.IsActive, batch.CreatedAt });
    }
}
