using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Exhibitors;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>ExhibitorMembership EF config. <see cref="ExhibitorMembership.UserId"/>
/// is a logical FK to <c>SimfUser.Id</c> on the Identity database, which is a
/// physically separate database, so there is NO HasOne navigation to
/// SimfUser and NO DB-level FK constraint — the link is by Guid only. Indexed
/// on ExhibitorId (list accounts under an exhibitor) and on UserId (resolve the
/// exhibitor a given account belongs to). Auto-discovered by
/// SimfAppDbContext.OnModelCreating via ApplyConfigurationsFromAssembly.</summary>
internal sealed class ExhibitorMembershipConfiguration
    : IEntityTypeConfiguration<ExhibitorMembership>
{
    public void Configure(EntityTypeBuilder<ExhibitorMembership> builder)
    {
        builder.ToTable("ExhibitorMemberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.ContactName).HasMaxLength(256).IsRequired();
        builder.Property(membership => membership.RoleLabel).HasMaxLength(128);

        // Exhibitor and ExhibitorMembership are both on the App
        // DB, so make the relationship an explicit Restrict FK (was Cascade by
        // convention): deleting an Exhibitor must not silently delete its member
        // accounts. The FK auto-creates the ExhibitorId index, so the former
        // standalone HasIndex(ExhibitorId) is dropped to avoid a duplicate.
        builder.HasOne(membership => membership.Exhibitor)
            .WithMany()
            .HasForeignKey(membership => membership.ExhibitorId)
            .OnDelete(DeleteBehavior.Restrict);

        // A UserId is provisioned as a single-use account, so at
        // most one ACTIVE membership per user: filtered unique backstop.
        builder.HasIndex(membership => membership.UserId)
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
