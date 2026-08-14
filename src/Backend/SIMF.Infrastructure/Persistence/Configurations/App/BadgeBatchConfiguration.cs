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

        builder.Property(batch => batch.Name).HasMaxLength(200).IsRequired();
        builder.Property(batch => batch.NameArabic).HasMaxLength(200).IsRequired();
        builder.Property(batch => batch.CountsSummary).HasMaxLength(512).IsRequired();
        // Matches the organiser-email cap validated in BulkGenerateBadgesAsync.
        builder.Property(batch => batch.RecipientEmail).HasMaxLength(256);

        builder.HasIndex(batch => new { batch.IsActive, batch.CreatedAt });

        // The order everyone who arrived without a bulk one belongs to, seeded in
        // EF model data rather than by a service so it exists the instant the
        // database does. That ordering is not a preference: UserProfile.BadgeBatchId
        // is a required foreign key, so the very first registration would fail
        // against an empty table.
        //
        // A fixed date, not "now", because HasData is compared into the migration
        // and a moving value would make every model check report a pending change.
        builder.HasData(new BadgeBatch
        {
            Id = BadgeBatch.DirectRegistrationId,
            Name = "Direct registration",
            NameArabic = "تسجيل مباشر",
            CountsSummary = "Direct registration",
            TotalCount = 0,
            IsDelegate = false,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0),
        });
    }
}
